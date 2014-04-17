using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NLog;
using System.Data.SqlClient;
using System.Data;
using System.Data.SqlTypes;
using System.Threading;
using System.IO;
using System.Transactions;
using System.Collections;
using System.Diagnostics;
using NGinnBPM.MessageBus.Queues;
using System.Reflection;

namespace NGinnBPM.MessageBus.Impl
{
    /// <summary>
    /// New version of sql message transport
    /// Makes propert use of transaction scopes
    /// </summary>
    public class SqlMessageTransport2 : IStartableService, IMessageTransport, IHealthCheck, IQueueOperations
    {
        #region IMessageTransport Members

        public void Send(MessageContainer message)
        {
            List<MessageContainer> lst = new List<MessageContainer>();
            lst.Add(message);
            SendBatch(lst, null);
        }

        public virtual void SendBatch(IList<MessageContainer> messages, object conn)
        {
            var sc = conn as IDbConnection;
            if (sc == null && AllowUseOfApplicationDbConnectionForSending)
            {
                sc = MessageBusContext.AppManagedConnection as IDbConnection;
            }

            if (sc != null 
                && SqlUtil.IsSameDatabaseConnection(sc.ConnectionString, ConnectionString)
                && sc.State == ConnectionState.Open)
            {
                InsertMessageBatchToLocalQueues(sc, messages);
            }
            else
            {
                if (AllowUseOfApplicationDbConnectionForSending && sc != null)
                {
                    log.Debug("*Not sharing the connection");
                }
                InsertMessageBatchToLocalQueues(messages);
            }
            Wakeup();
        }

       
        public event MessageArrived OnMessageArrived;
        public event MessageArrived OnMessageToUnknownDestination;

        #endregion

        protected Logger log = LogManager.GetCurrentClassLogger();
        protected Logger statLog = LogManager.GetLogger("STAT.SqlMessageTransport2");
        
        private string _connAlias;
        private Dictionary<string, string> _connStrings = new Dictionary<string,string>();
        private string _queueTable = "MessageQueue";


        public virtual string Endpoint
        {
            get { return string.Format("sql://{0}/{1}", _connAlias, _queueTable); }
            set 
            {
                string alias, table;
                if (!SqlUtil.ParseSqlEndpoint(value, out alias, out table))
                    throw new Exception("Invalid endpoint");
                _connAlias = alias;
                _queueTable = table;
                log = LogManager.GetLogger("SQLMT_" + Endpoint);
                statLog = LogManager.GetLogger("STATSQLMT_" + Endpoint);
                if (Name == null) Name = value;
            }
        }
        /// <summary>
        /// True if the db connection used to receive current message
        /// should also be used for sending messages.
        /// This way you have a transactional receive and send without employing a distributed 
        /// transaction.
        /// </summary>
        public bool UseReceiveTransactionForSending { get; set; }
        /// <summary>
        /// If true, local messages will be inserted directly to their destination tables.
        /// Local messages are the ones that don't leave the database (sender and recipient are in the same database
        /// but may use different tables).
        /// If false, all messages will go thru local queue first.
        /// </summary>
        public bool SendLocalMessagesDirectly { get; set; }
        /// <summary>
        /// Will not receive messages - send only
        /// </summary>
        public bool SendOnly { get; set; }
        /// <summary>
        /// Default timeout for message receive transaction
        /// If message handling takes longer than the timeout value the transaction
        /// will be aborted. So better be quick with messages.
        /// </summary>
        public TimeSpan DefaultTransactionTimeout { get; set; }
        /// <summary>
        /// Maximum number of parameters in SQL insert query
        /// </summary>
        public int MaxSqlParamsInBatch { get; set; }
        /// <summary>
        /// Throttling. Maximum message receiving frequency.
        /// </summary>
        public double? MaxReceiveFrequency { get; set; }
        /// <summary>
        /// Map alias->connection string used for mapping endpoint name to a database
        /// </summary>
        public IDictionary<string, string> ConnectionStrings
        {
            get { return _connStrings; }
            set { _connStrings = new Dictionary<string,string>(value); }
        }

        public IDictionary ConnectionStringDictionary
        {
            get { return _connStrings; }
            set 
            { 
                _connStrings = new Dictionary<string, string>();
                foreach (string k in value.Keys)
                    _connStrings[k] = (string)value[k];
            }
        }

        /// <summary>
        /// Connection string for current endpoint
        /// </summary>
        public string ConnectionString
        {
            get
            {
                string connstr;
                if (!_connStrings.TryGetValue(_connAlias, out connstr)) return null;
                return connstr;
            }
        }

        /// <summary>
        /// Message sequence manager to be used
        /// </summary>
        public ISequenceMessages SequenceManager { get; set; }

        private Thread _processorThread;
        private List<Thread> _messageHandlerThreads = new List<Thread>();

        private bool _stop = false;
        private EventWaitHandle _waiter = new AutoResetEvent(true);
        private int _maxConcurrentMessages = 5;

        private Random _rand = new Random();

        public delegate void MessageContainerHandler(MessageContainer mc);
        public delegate void MessageContainerFailureHandler(MessageContainer mc, Exception error);

        public event MessageContainerFailureHandler MessageFailedAllRetries;
        public event MessageContainerFailureHandler MessageFailed;

        public TimeSpan[] RetryTimes
        {
            get { return _retryTimes; }
            set
            {
                if (value == null) throw new ArgumentNullException();
                _retryTimes = value;
            }
        }
        /// <summary>
        /// Array of retry times - this defines what will be the
        /// delay between subsequent message retries
        /// </summary>
        protected TimeSpan[] _retryTimes = new TimeSpan[] {
            TimeSpan.FromSeconds(30),
            TimeSpan.FromMinutes(2),
            TimeSpan.FromMinutes(5),
            TimeSpan.FromMinutes(10),
            TimeSpan.FromMinutes(30),
            TimeSpan.FromHours(2),
            TimeSpan.FromHours(8),
            TimeSpan.FromHours(16),
            TimeSpan.FromHours(36),
            TimeSpan.FromDays(3)
        };

        public SqlMessageTransport2()
        {
            MessageRetentionPeriod = TimeSpan.FromHours(12);
            DefaultTransactionTimeout = TimeSpan.FromMinutes(1);
            SendLocalMessagesDirectly = true;
            AllowUseOfApplicationDbConnectionForSending = true;
            UseReceiveTransactionForSending = true;
            ExposeReceiveConnection = true;
            SendOnly = false;
            MaxMessagesPerSingleConnection = 50;
            MaxSqlParamsInBatch = 200;
            //MaxReceiveFrequency = 1000;
        }

        static SqlMessageTransport2()
        {
            System.Transactions.TransactionManager.DistributedTransactionStarted += new TransactionStartedEventHandler(TransactionManager_DistributedTransactionStarted);
        }

        static void TransactionManager_DistributedTransactionStarted(object sender, TransactionEventArgs e)
        {
            var log = LogManager.GetLogger("DTC");
            log.Info("***\nDistributed transaction started (Message: {1})! {0}***\n", e.Transaction.TransactionInformation.LocalIdentifier, _curMsg == null ? "none" : _curMsg.Message.BusMessageId);
            if (log.IsDebugEnabled)
            {
                log.Debug("DT stack: {0}", Environment.StackTrace);
            }
        }

        
        /// <summary>
        /// Set to true if you want the message queue table to be 
        /// automatically created
        /// </summary>
        public bool AutoCreateQueueTable
        {
            get;
            set;
        }
        /// <summary>
        /// Set to true to allow sql transport to use a db connection supplied 
        /// by the application for sending messages
        /// </summary>
        public bool AllowUseOfApplicationDbConnectionForSending { get; set; }
        public bool ExposeReceiveConnection { get; set; }
        /// <summary>
        /// Set to true to pause processing
        /// of queued messages for some time
        /// </summary>
        public bool PauseMessageProcessing
        {
            get;
            set;
        }

        /// <summary>
        /// Max sequence of messages that can be received without closing and re-opening a connection.
        /// </summary>
        public int MaxMessagesPerSingleConnection { get; set; }
        

        /// <summary>
        /// Amount of time processed messages are kept in database.
        /// If this is TimeSpan.Zero messages are deleted immediately after being handled
        /// </summary>
        public TimeSpan MessageRetentionPeriod
        {
            get;
            set;
        }

        /// <summary>
        /// Open database connection
        /// </summary>
        /// <param name="connString"></param>
        /// <returns></returns>
        private static SqlConnection OpenConnection(string connString)
        {
            SqlConnection conn = new System.Data.SqlClient.SqlConnection(connString);
            conn.Open();
            return conn;
        }

        private SqlConnection OpenConnection()
        {
            string connstr;
            if (!_connStrings.TryGetValue(_connAlias, out connstr))
                throw new Exception("No connection string for alias " + _connAlias);
            return OpenConnection(connstr);
        }


        /// <summary>
        /// Number of message processing threads
        /// </summary>
        public int MaxConcurrentMessages
        {
            get { return _maxConcurrentMessages; }
            set { _maxConcurrentMessages = value; }
        }
        
        /// <summary>
        /// Current size of retry queue
        /// </summary>
        public int RetryQueueSize
        {
            get 
            {
                using (IDbConnection con = OpenConnection())
                {
                    string sql = string.Format("select count(*) from {0} with(nolock) where subqueue='R'", _queueTable);
                    using (IDbCommand cmd = con.CreateCommand())
                    {
                        cmd.CommandText = sql;
                        return Convert.ToInt32(cmd.ExecuteScalar());
                    }
                }
            }
        }


        /// <summary>
        /// Current size of 'Failed' subqueue
        /// </summary>
        public int FailQueueSize
        {
            get
            {
                using (IDbConnection con = OpenConnection())
                {
                    string sql = string.Format("select count(*) from {0} with(nolock) where subqueue='F'", _queueTable); 
                    using (IDbCommand cmd = con.CreateCommand())
                    {
                        cmd.CommandText = sql;
                        return Convert.ToInt32(cmd.ExecuteScalar());
                    }
                }
            }
        }

        public long AverageLatencyMs
        {
            get
            {
                using (IDbConnection con = OpenConnection())
                {
                    string sql = string.Format("select coalesce(avg(DATEDIFF(millisecond, retry_time, last_processed)), 0) from {0} with(nolock) where retry_time >= @time_limit and subqueue='X'", _queueTable);
                    using (IDbCommand cmd = con.CreateCommand())
                    {
                        cmd.CommandText = sql;
                        SqlUtil.AddParameter(cmd, "@time_limit", DateTime.Now.AddMinutes(-5));
                        return Convert.ToInt64(cmd.ExecuteScalar());
                    }
                }

            }
        }
        /// <summary>
        /// Current size of input queue
        /// </summary>
        public int InputQueueSize
        {
            get
            {
                using (IDbConnection con = OpenConnection())
                {
                    string sql = string.Format("select count(*) from {0} with(nolock) where subqueue='I'", _queueTable);
                    using (IDbCommand cmd = con.CreateCommand())
                    {
                        cmd.CommandText = sql;
                        return Convert.ToInt32(cmd.ExecuteScalar());
                    }
                }
            }
        }
        /// <summary>
        /// Move failed messages back to input subqueue
        /// </summary>
        public void RetryFailedMessages()
        {
            using (IDbConnection conn = OpenConnection())
            {
                using (IDbCommand cmd = conn.CreateCommand())
                {
                    cmd.CommandText = string.Format("update {0} with(READPAST) set subqueue='I', retry_count=0, error_info=null where subqueue='F'", _queueTable);
                    int rows = cmd.ExecuteNonQuery();
                    log.Info("{0} messages returned to queue {1}", rows, _queueTable);
                }
            }
        }

        
        /// <summary>
        /// Create the message queue table
        /// </summary>
        protected virtual void InitializeQueueTableIfDoesntExist()
        {
            using (Stream stm = typeof(SqlMessageTransport2).Assembly.GetManifestResourceStream("NGinnBPM.MessageBus.createmqueue.mssql.sql"))
            {
                StreamReader sr = new StreamReader(stm);
                using (IDbConnection conn = OpenConnection())
                {
                    using (IDbCommand cmd = conn.CreateCommand())
                    {
                        string txt = sr.ReadToEnd();
                        cmd.CommandText = string.Format(txt, _queueTable);
                        cmd.ExecuteNonQuery();
                    }
                }
            }

        }

        /// <summary>
        /// Start processing incoming messages
        /// </summary>
        public virtual void Start()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var attr = Attribute.GetCustomAttribute(assembly, typeof(AssemblyFileVersionAttribute)) as AssemblyFileVersionAttribute;
            log.Info("NGinn MessageBus v {0} starting SQL transport for endpoint {1}", attr == null ? "---" : attr.Version, Endpoint);

            lock (this)
            {
                if (OnMessageArrived == null) 
                    throw new Exception("OnMessageArrived not configured for sql transport " + this.Endpoint);

                if (MaxConcurrentMessages < 1 || MaxConcurrentMessages > 50)
                    throw new Exception("Set MaxConcurrentMessages to value between 1 and 50");

                if (SendOnly)
                {
                    log.Info("Starting the message bus in send only mode");
                }
                else
                {
                    if (AutoCreateQueueTable)
                    {
                        InitializeQueueTableIfDoesntExist();
                    }
                    if (_processorThread == null)
                    {
                        _stop = false;
                        for (int i = 0; i < MaxConcurrentMessages; i++)
                        {
                            Thread thr = new Thread(this.MessageProcessingThreadLoop);
                            thr.Name = "NGinnBPM.MessageHandler_" + i;
                            thr.IsBackground = true;
                            _messageHandlerThreads.Add(thr);
                            log.Info("Created message handler thread " + thr.Name);
                        }
                        foreach (Thread thr in _messageHandlerThreads)
                        {
                            log.Info("Starting message handler thread " + thr.Name);
                            thr.Start();
                        }
                        _waiter.Set();
                    }

                    if (_processorThread == null)
                    {
                        _processorThread = new Thread(new ThreadStart(this.CleanupThreadLoop));
                        _processorThread.Name = "NGinnBPM.MessageBus cleanup thread";
                        _processorThread.IsBackground = true;
                        _processorThread.Start();
                    }
                }
            
            }
        }

        public virtual void Stop()
        {
            lock (this)
            {
                _stop = true;
                foreach (Thread thr in _messageHandlerThreads)
                {
                    log.Debug("Interrupting message handler thread {0}", thr.Name);
                    thr.Interrupt();
                }
				if (_processorThread != null)
                {
                    log.Debug("Stopping manager thread {0}", _processorThread.Name);
                    _processorThread.Interrupt();
                    _processorThread.Join();
                    _processorThread = null;
                    log.Debug("Manager thread stopped");
                }
                foreach (Thread thr in _messageHandlerThreads)
                {
                    thr.Join();
                    log.Debug("Stopped message handler thread {0}", thr.Name);
                }
                _messageHandlerThreads = new List<Thread>();
            }
        }

        public bool IsRunning
        {
            get { return _processorThread != null; }
        }

        /// <summary>
        /// Wake up message processing thread
        /// </summary>
        private void Wakeup()
        {
            _waiter.Set();
        }

        protected virtual void DetectStuckMessages()
        {
            lock (_nowProcessing)
            {
                foreach (var kv in _nowProcessing)
                {
                    if (kv.Value.AddSeconds(120) < DateTime.Now)
                    {
                        log.Warn("Message {0} is still being processed since {1}", kv.Key, kv.Value);
                    }
                }
            }
        }
        /// <summary>
        /// Cleanup thread procedure
        /// Removes old messages and handles 'retry' messages
        /// </summary>
        protected virtual void CleanupThreadLoop()
        {
            DateTime lastCleanup = DateTime.Now;
            NLog.MappedDiagnosticsContext.Set("nmbendpoint", Endpoint.Replace('/', '_').Replace(':', '_'));
            log.Info("Cleanup thread started");
            Thread.Sleep(2000);
            while (!_stop)
            {
                try
                {
                    if (ProcessRetryMessages())
                    {
                        Wakeup();
                    }
                    if (!_stop && (DateTime.Now - lastCleanup).TotalMinutes > 1.03)
                    {
                        CleanupProcessedMessages();
                        lastCleanup = DateTime.Now;
                        DetectStuckMessages();
                    }
                    Thread.Sleep(TimeSpan.FromSeconds(8.39));
                }
                catch (ThreadInterruptedException)
                {
                    log.Info("Cleanup  thread interrupted");
                }
                catch (ThreadAbortException ex)
                {
                    log.Info("Cleanup  thread aborted {0}", ex);
                    throw;
                }
                catch (Exception ex)
                {
                    log.Error("Cleanup  thread error - pausing execution: {0}", ex);
                    Thread.Sleep(TimeSpan.FromMinutes(2));
                }
            }
        }

        /// <summary>
        /// Message handler thread procedure. 
        /// Handles incoming messages in a loop. Can be run by multiple threads.
        /// </summary>
        protected virtual void MessageProcessingThreadLoop()
        {
#warning TODO add frequency throttling
            log.Info("Processing thread {0} started", Thread.CurrentThread.ManagedThreadId);
            NLog.MappedDiagnosticsContext.Set("nmbendpoint", Endpoint.Replace('/', '_').Replace(':', '_'));
            Thread.Sleep(2000);
            while (!_stop)
            {
                try
                {
                    SqlConnection cn = OpenConnection();
                    bool pause = true;
                    int delayMs = 0;
                    try
                    {
                        CurrentConnection = cn;
                        if (ExposeReceiveConnection) MessageBusContext.ReceivingConnection = cn;
                        int cnt = 0;
                        while (!_stop && ProcessNextMessage(cn))
                        {
                            if (cnt++ > MaxMessagesPerSingleConnection)
                            {
                                pause = false;
                                break;
                            }
                            if (MaxReceiveFrequency.HasValue)
                            {
                                double curFreq = 0;
                                double window = 0;
                                lock (_nowProcessing)
                                {
                                    int fcnt = _frequency.Count;
                                    if (fcnt == 0) continue;
                                    long st = _frequency.Peek();
                                    window = _freqSw.ElapsedTicks - st;
                                    curFreq = ((double)fcnt * Stopwatch.Frequency) / window;
                                }
                                log.Info("Current frequency is {0}", curFreq);
                                if (curFreq > MaxReceiveFrequency.Value)
                                {
                                    //calculate delay time so that maximum frequency is not exceeded
                                    pause = true;
                                    delayMs = 1;
                                }
                            }
                        }
                    }
                    finally
                    {
                        if (ExposeReceiveConnection) MessageBusContext.ReceivingConnection = null;
                        CurrentConnection = null;
                        cn.Dispose();
                    }
                    
                    if (pause && !_stop)
                    {
                        TimeSpan tt = delayMs > 0 ? TimeSpan.FromMilliseconds(delayMs) : TimeSpan.FromSeconds(5 + _rand.Next(1, 10));
                        bool b = _waiter.WaitOne(tt);
                    }
                }
                catch (ThreadInterruptedException)
                {
                }
                catch (ThreadAbortException)
                {
                    log.Warn("Thread abort in message processing thread");
                    throw;
                }
                catch (Exception ex)
                {
                    log.Error("Message processing thread error. Pausing execution for some time: {0}", ex);
                    if (!_stop) Thread.Sleep(TimeSpan.FromSeconds(123));
                }
            }
            log.Info("Message processing thread {0} exiting ({1})", Thread.CurrentThread.ManagedThreadId, Endpoint);
        }
        
        /// <summary>
        /// Alternative version that selects & updates the row in a single query.
        /// However, the testing has shown that it's actually slower than the original, two-query, version
        /// I'm leaving it here to remember that this has already been tried and failed.
        /// </summary>
        /// <param name="conn"></param>
        /// <param name="retryTime"></param>
        /// <returns></returns>
        private MessageContainer SelectNextMessageForProcessing2008(IDbConnection conn, out DateTime? retryTime)
        {
            var mc = new MessageContainer();
            retryTime = null;
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = string.Format(@"UPDATE TOP(1) {0} with(readpast)
                    SET subqueue = 'X', last_processed = getdate()
                    OUTPUT
                     inserted.id, inserted.from_endpoint, inserted.to_endpoint, inserted.retry_count, inserted.retry_time, inserted.correlation_id, inserted.msg_text, inserted.msg_headers, inserted.unique_id
                    WHERE
                    id in (select top(1) id from {0} with(readpast) where subqueue = 'I' order by retry_time)", _queueTable);
                using (var dr = cmd.ExecuteReader())
                {
                    if (!dr.Read()) return null;
                    mc.From = Convert.ToString(dr["from_endpoint"]);
                    mc.To = Convert.ToString(dr["to_endpoint"]);
                    mc.HeadersString = Convert.ToString(dr["msg_headers"]);
                    mc.SetHeader(MessageContainer.HDR_RetryCount, Convert.ToInt32(dr["retry_count"]).ToString()); ;
                    mc.CorrelationId = Convert.ToString(dr["correlation_id"]);
                    mc.BusMessageId = Convert.ToString(dr["id"]);
                    mc.UniqueId = Convert.ToString(dr["unique_id"]);
                    retryTime = Convert.ToDateTime(dr["retry_time"]);
                    mc.BodyStr = dr.GetString(dr.GetOrdinal("msg_text"));                                
                }
            }
            return mc;
        }
 
        private MessageContainer SelectNextMessageForProcessing(IDbConnection conn, out DateTime? retryTime, out bool moreMessages)
        {
            retryTime = null;
            var mc = new MessageContainer();
            moreMessages = false;        
            using (IDbCommand cmd = conn.CreateCommand())
            {
                string sql = string.Format("select top 1 id, correlation_id, from_endpoint, to_endpoint, retry_count, msg_text, msg_headers, unique_id, retry_time from {0} with (UPDLOCK, READPAST) where subqueue='I' order by retry_time", _queueTable);
                cmd.CommandText = sql;

                using (IDataReader dr = cmd.ExecuteReader())
                {
                    if (!dr.Read()) return null;
                    var rc = Convert.ToInt32(dr["retry_count"]);
                    mc.From = Convert.ToString(dr["from_endpoint"]);
                    mc.To = Convert.ToString(dr["to_endpoint"]);
                    mc.HeadersString = Convert.ToString(dr["msg_headers"]);
                    mc.SetHeader(MessageContainer.HDR_RetryCount, rc.ToString()); ;
                    mc.CorrelationId = Convert.ToString(dr["correlation_id"]);
                    mc.BusMessageId = Convert.ToString(dr["id"]);
                    mc.UniqueId = Convert.ToString(dr["unique_id"]);
                    retryTime = Convert.ToDateTime(dr["retry_time"]);
                    mc.BodyStr = dr.GetString(dr.GetOrdinal("msg_text"));
                    mc.IsFinalRetry = rc < _retryTimes.Length;
                }
                cmd.CommandText = string.Format("update {0} with(readpast, rowlock) set subqueue='X', last_processed = getdate() where id=@id and subqueue='I'", _queueTable);
                SqlUtil.AddParameter(cmd, "@id", Int64.Parse(mc.BusMessageId));
                int cnt = cmd.ExecuteNonQuery();
                if (cnt == 0)
                {
                    log.Warn("Updated 0 rows when trying to lock message {0}. Skipping", mc.BusMessageId);
                    moreMessages = true;
                    return null;
                    
                }
                return mc;
            }
        }

        public bool UseSqlOutputClause { get; set; }

        private Dictionary<string, DateTime> _nowProcessing = new Dictionary<string, DateTime>();
        private Queue<long> _frequency = new Queue<long>();
        private Stopwatch _freqSw = Stopwatch.StartNew();

        /// <summary>
        /// Process next message from the queue
        /// </summary>
        /// <param name="conn">database connection (open)</param>
        /// <param name="pauseMs">
        /// returns number of milliseconds to pause before handling next message.
        /// This is used for message throttling.
        /// </param>
        /// <returns>
        /// true if there are more messages to process
        /// false if there are no more messages to process and the receiving thread
        /// should pause for some time.
        /// </returns>
        protected virtual bool ProcessNextMessage(SqlConnection conn)
        {
            var sw = Stopwatch.StartNew();
            string mtype = null;
            DateTime? retryTime = null;
            string id = null; string lbl = "";
            FailureDisposition doRetry = FailureDisposition.RetryIncrementRetryCount;
            DateTime? nextRetry = null;
            int retryCount = 0; bool messageFailed = false;
            bool abort = true; //by default, abort 
            Exception handlingError = null;
            
            try
            {
                TransactionOptions to = new TransactionOptions { IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted, Timeout = DefaultTransactionTimeout };
                using (TransactionScope ts = new TransactionScope(TransactionScopeOption.Required, to))
                {
                    conn.EnlistTransaction(Transaction.Current);
                    try
                    {
                        bool moreMessages = false;
                        var mc = UseSqlOutputClause ? SelectNextMessageForProcessing2008(conn, out retryTime) : SelectNextMessageForProcessing(conn, out retryTime, out moreMessages);
                        if (mc == null) return moreMessages;
                        NLog.MappedDiagnosticsContext.Set("nmbrecvmsg", mc.BusMessageId);
                        log.Debug("Selected message {0} for processing", mc.BusMessageId);
                        id = mc.BusMessageId;
                        lock (_nowProcessing)
                        {
                            _nowProcessing[id] = DateTime.Now;
                            _frequency.Enqueue(_freqSw.ElapsedTicks);
                            while (_frequency.Count > MaxConcurrentMessages) _frequency.Dequeue();
                        }

                        retryCount = mc.RetryCount;
                        
                        doRetry = retryCount < _retryTimes.Length ? FailureDisposition.RetryIncrementRetryCount : FailureDisposition.Fail;
                        nextRetry = doRetry == FailureDisposition.RetryIncrementRetryCount ? DateTime.Now + _retryTimes[retryCount] : (DateTime?)null;

                        _curMsg = new CurMsgInfo(mc);
                        if (retryTime.HasValue)
                        {
                            TimeSpan latency = DateTime.Now - retryTime.Value;
                            statLog.Info("LATENCY:{0}", (long) latency.TotalMilliseconds);
                        }
                        try
                        {
                            if (mc.HasHeader(MessageContainer.HDR_TTL))
                            {
                                var ttl = mc.GetDateTimeHeader(MessageContainer.HDR_TTL, DateTime.MaxValue);
                                if (ttl < DateTime.Now)
                                {
                                    log.Info("Message #{0} TTL expired", id);
                                    abort = false;
                                    return true;
                                }
                            }
                            if (!IsLocalEndpoint(mc.To))
                            {
                                ForwardMessageToRemoteEndpoint(mc);
                                abort = false;
                                return true;
                            }
                            if (mc.HasHeader(MessageContainer.HDR_SeqId) && SequenceManager != null)
                            {
                                var seqn = mc.SequenceNumber;
                                if (seqn < 0) throw new Exception("Invalid sequence ordinal number");

                                var md = SequenceManager.SequenceMessageArrived(mc.SequenceId, seqn, mc.SequenceLength, conn, id);
                                if (md.MessageDispositon == SequenceMessageDisposition.ProcessingDisposition.RetryImmediately)
                                {
                                    return true;
                                }
                                else if (md.MessageDispositon == SequenceMessageDisposition.ProcessingDisposition.Postpone)
                                {
                                    MarkMessageForProcessingLater(id, md.EstimatedRetry.HasValue ? md.EstimatedRetry.Value : DateTime.Now.AddMinutes(1), null, conn);
                                    abort = false; //save the transaction
                                    return true;
                                }
                                else if (md.MessageDispositon == SequenceMessageDisposition.ProcessingDisposition.HandleMessage)
                                {
                                    if (!string.IsNullOrEmpty(md.NextMessageId)) MoveRMessageToInputQueue(md.NextMessageId, conn);
                                }
                                else throw new Exception();
                            }

                            //log.Trace("Processing message {0} locally", mc.BusMessageId);
                            if (OnMessageArrived != null)
                            {
                                OnMessageArrived(mc, this);
                                if (mc.Body != null) mtype = mc.Body.GetType().Name;
                            }
                            else
                            {
                                throw new Exception("OnMessageArrived not configured for Sql transport " + Endpoint);
                            }
                            abort = false;

                            if (_curMsg.ProcessLater.HasValue)
                            {
                                if (_curMsg.ProcessLater.Value <= DateTime.Now)
                                {
                                    abort = true;
                                }
                                else
                                {
                                    MarkMessageForProcessingLater(id, _curMsg.ProcessLater.Value, null, conn);
                                }
                            }
                            if (Transaction.Current.TransactionInformation.Status == TransactionStatus.Aborted)
                            {
                                throw new Exception("Current transaction has aborted without an exception (probably because inner TransactionScope has aborted)");
                            }
                            return true;
                        }
                        catch (ThreadAbortException)
                        {
                            log.Warn("ThreadAbort when processing message");
                            abort = true;
                            throw;
                        }
                        catch (RetryMessageProcessingException ex)
                        {
                            log.Info("Retry message processing at {1}: {0}", ex.Message, ex.RetryTime);
                            abort = true;
                            doRetry = FailureDisposition.RetryDontIncrementRetryCount;
                            nextRetry = ex.RetryTime;
                        }
                        catch (Exception ex)
                        {
                            abort = true;
                            messageFailed = true;
                            log.Warn("Error processing message {0}: {1}", id, ex);
                            handlingError = ex;
                            if (ex is System.Reflection.TargetInvocationException)
                            {
                                handlingError = ex.InnerException;
                            }
                            else if (ex is PermanentMessageProcessingException)
                            {
                                if (ex.InnerException != null) handlingError = ex.InnerException;
                                doRetry = FailureDisposition.Fail;
                            }
                            if (MessageFailed != null) MessageFailed(mc, handlingError);
                            if (doRetry == FailureDisposition.Fail)
                            {
                                if (MessageFailedAllRetries != null) MessageFailedAllRetries(mc, handlingError);
                            }
                        }
                        finally
                        {
                            _curMsg = null;
                        }

                    }
                    catch (Exception ex)
                    {
                        log.Error("Unexpected error processing message {0}: {1}", id, ex.ToString());
                        abort = true;
                        throw new Exception("Unexpected error", ex);
                    }
                    finally
                    {
                        if (!abort)
                        {
                            ts.Complete();
                        }
                    }
                } //end transaction 1
                
                if (abort && messageFailed)
                {
                    ///here we have a race condition - previous transaction was rolled back
                    ///and new transaction hasn't started yet so we don't hold a lock on the
                    ///message record and someone may snatch it in the meantime
                    ///But we shouldn't worry too much, if someone steals the message he
                    ///will be responsible for updating its status
                    using (var ts = new TransactionScope(TransactionScopeOption.Required, to))
                    {
                        conn.EnlistTransaction(Transaction.Current);
                        if (MarkMessageFailed(id, handlingError.ToString(), doRetry, nextRetry.HasValue ? nextRetry.Value : DateTime.Now, conn))
                        {
                            log.Info("Message {0}  marked {1} because of  failure. Retry number: {2}", id, doRetry, retryCount);
                        }
                        ts.Complete();
                    }
                }
                return false;
            }
            finally
            {
                if (!string.IsNullOrEmpty(id))
                {
                    lock (_nowProcessing)
                    {
                        _nowProcessing.Remove(id);
                    }
                    sw.Stop();
                    log.Log(sw.ElapsedMilliseconds > 2000 ? LogLevel.Warn : LogLevel.Info, "ProcessNextMessage {0} took {1} ms", id, sw.ElapsedMilliseconds);
                    statLog.Info("ProcessNextMessage:{0}", sw.ElapsedMilliseconds);
                    if (!string.IsNullOrEmpty(mtype))
                    {
                        statLog.Info("ProcessMessage_{0}:{1}", mtype, sw.ElapsedMilliseconds);
                    }
                }
                NLog.MappedDiagnosticsContext.Remove("nmbrecvmsg");
            }
        }

        private enum FailureDisposition
        {
            Fail,
            RetryIncrementRetryCount,
            RetryDontIncrementRetryCount
        }

		private bool MarkMessageFailed(string id, string errorInfo, FailureDisposition disp, DateTime retryTime, IDbConnection conn)
        {
            DateTime t0 = DateTime.Now;
            bool ret = true;
            string sql = "update {0} with(readpast, rowlock) set retry_count = retry_count + {1}, retry_time=@retry_time, error_info=@error_info, last_processed=getdate(), subqueue=@subq where id=@id";
            sql = string.Format(sql, _queueTable, disp == FailureDisposition.RetryDontIncrementRetryCount ? 0 : 1);
            using (IDbCommand cmd = conn.CreateCommand())
            {
                cmd.CommandText = sql;
                SqlUtil.AddParameter(cmd, "@id", Convert.ToInt64(id));
                SqlUtil.AddParameter(cmd, "@retry_time", retryTime);
                SqlUtil.AddParameter(cmd, "@error_info", errorInfo);
                SqlUtil.AddParameter(cmd, "@subq", disp == FailureDisposition.Fail ? "F" : "R");
                int n = cmd.ExecuteNonQuery();
                if (n != 1)
                {
                    log.Warn("Failed to mark message {0} for retry. Probably someone else is handling it now...", id);
                    ret = false;
                }
            }
            TimeSpan ts = DateTime.Now - t0;
            log.Log(ts.TotalMilliseconds > 50.0 ? LogLevel.Warn : LogLevel.Trace, "MarkMessageFailed2 {0} update time: {1}", id, ts);
            return ret;
        }
        /// <summary>
        /// Moves a waiting message (R) to input queue
        /// </summary>
        /// <param name="id"></param>
        /// <param name="tran"></param>
        /// <returns>true if message was moved</returns>
        private bool MoveRMessageToInputQueue(string id, IDbConnection con)
        {
            DateTime t0 = DateTime.Now;
            string sql = "update {0} with(readpast, rowlock) set subqueue='I' where id=@id and subqueue='R'";

            sql = string.Format(sql, _queueTable);
            using (IDbCommand cmd = con.CreateCommand())
            {
                cmd.CommandText = sql;
                SqlUtil.AddParameter(cmd, "@id", Convert.ToInt64(id));
                int n = cmd.ExecuteNonQuery();
                return n > 0;
            }
        }

        


        /// <summary>
        /// Process the message later, at specified time. Does not increase the retry count so can be done any number of times.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="retryTime"></param>
        /// <param name="tran"></param>
        private void MarkMessageForProcessingLater(string id, DateTime retryTime, int? retryCount, IDbConnection con)
        {
            DateTime t0 = DateTime.Now;
            string sql = "update {0} with(rowlock) set retry_time=@retry_time, last_processed=getdate(), subqueue='R' where id=@id";
            sql = string.Format(sql, _queueTable);
            using (IDbCommand cmd = con.CreateCommand())
            {
                cmd.CommandText = sql;
                SqlUtil.AddParameter(cmd, "@id", Convert.ToInt64(id));
                SqlUtil.AddParameter(cmd, "@retry_time", retryTime);
                int n = cmd.ExecuteNonQuery();
                if (n != 1) throw new Exception(string.Format("Failed to update message {0} when moving to retry queue", id));
            }
            TimeSpan ts = DateTime.Now - t0;
            log.Log(ts.TotalMilliseconds > 50.0 ? LogLevel.Warn : LogLevel.Trace, "MarkMessageForProcessingLater {0} update time: {1}", id, ts);
        }


        /// <summary>
        /// Delete old handled messages so the database doesn't grow too large
        /// </summary>
        public virtual void CleanupProcessedMessages()
        {
            try
            {
                DateTime t0 = DateTime.Now;
                DateTime lmt = DateTime.Now - MessageRetentionPeriod;
                using (IDbConnection conn = OpenConnection())
                {
                    using (IDbCommand cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = string.Format("delete top(10000) {0} with(READPAST) where retry_time <= @lmt and subqueue='X'", _queueTable);
                        SqlUtil.AddParameter(cmd, "@lmt", lmt);
                        int n = cmd.ExecuteNonQuery();
                        log.Info("Deleted {0} messages", n);
                    }
                }
                TimeSpan ts = DateTime.Now - t0;
                log.Log(ts.TotalMilliseconds > 2000.0 ? LogLevel.Warn : LogLevel.Trace, "CleanupProcessedMessages update time: {0}", ts);
            }
            catch(Exception ex)
            {
                log.Error("Error deleting processed messages: {0}", ex);
            }
        }

        private class CurMsgInfo
        {
            internal MessageContainer Message { get; set; }
            internal DateTime? ProcessLater { get; set; }
            internal int? ThrottleDelayMs { get; set; }

            internal CurMsgInfo(MessageContainer mc)
            {
                Message = mc;
            }
        }
       

        /// <summary>
        /// Forwards message to a remote endpoint
        /// </summary>
        /// <param name="mc"></param>
        protected virtual void ForwardMessageToRemoteEndpoint(MessageContainer mc)
        {
            if (mc.To.StartsWith("sql://"))
            {
                string alias, table;
                if (!SqlUtil.ParseSqlEndpoint(mc.To, out alias, out table))
                    throw new Exception("Invalid target endpoint: " + mc.To);
                List<MessageContainer> l = new List<MessageContainer>();
                l.Add(mc);
                var d = new Dictionary<string, ICollection<MessageContainer>>();
                d[table] = l;
                if (!ConnectionStrings.ContainsKey(alias)) throw new Exception("Unknown connection string alias: " + alias);
                InsertMessageBatchToLocalDatabaseQueues(this.ConnectionStrings[alias], d);
            }
            else
            {
                if (OnMessageToUnknownDestination != null)
                {
                    OnMessageToUnknownDestination(mc, this);
                }
                else throw new Exception("Don't know how to send message to destination: " + mc.To);
            }
        }

        /// <summary>
        /// Move scheduled messages to input subqueue if their delivery date has passed
        /// 
        /// </summary>
        /// <returns></returns>
        protected virtual bool ProcessRetryMessages()
        {
            DateTime st = DateTime.Now;
            try
            {

                string sql = string.Format("update top (1000) {0} with(READPAST) set subqueue='I' where subqueue='R' and retry_time <= getdate()", _queueTable);
                using (IDbConnection conn = OpenConnection())
                {
                    using (IDbCommand cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = sql;
                        int n = cmd.ExecuteNonQuery();
                        if (n > 0)
                        {
                            log.Info("Moved {0} messages from retry to input in queue {1}", n, _queueTable);
                            return true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error("Error processing retry messages: {0}", ex);
            }
            finally
            {
                TimeSpan ts = DateTime.Now - st;
                log.Log(ts.TotalMilliseconds > 100 ? LogLevel.Warn : LogLevel.Trace, "ProcessRetryMessages time: {0}", ts);
                statLog.Info("ProcessRetryMessages:{0}", (int) ts.TotalMilliseconds);
            }
            return false;
        }

        

        

        ~SqlMessageTransport2()
        {
            Stop();
        }
        /// <summary>
        /// Remove nginn message headers that are kept in dedicated db columns
        /// </summary>
        /// <param name="msgHeaders"></param>
        /// <returns></returns>
        protected static Dictionary<string, string> RemoveNGHeaders(Dictionary<string, string> msgHeaders)
        {
            Dictionary<string, string> d = new Dictionary<string, string>();
            if (msgHeaders == null) return d;
            foreach (string k in msgHeaders.Keys)
            {
                if (k != MessageContainer.HDR_BusMessageId &&
                    k != MessageContainer.HDR_ContentType &&
                    k != MessageContainer.HDR_CorrelationId &&
                    k != MessageContainer.HDR_DeliverAt &&
                    k != MessageContainer.HDR_Label &&
                    k != MessageContainer.HDR_RetryCount)
                    d[k] = msgHeaders[k];
            }
            return d;
        }

        /// <summary>
        /// Convert message headers to a string
        /// </summary>
        /// <param name="headers"></param>
        /// <returns></returns>
        protected static string HeadersToString(Dictionary<string, string> headers)
        {
            StringBuilder sb = new StringBuilder();
            foreach (string k in headers.Keys)
            {
                string v = headers[k];
                if (sb.Length > 0) sb.Append("|");
                sb.Append(string.Format("{0}={1}", k, headers[k]));
            }
            return sb.ToString();
        }

        protected static Dictionary<string, string> StringToHeaders(string hdr)
        {
            Dictionary<string, string> Headers = new Dictionary<string, string>();
            if (hdr == null) return Headers;
            if (hdr.Length == 0) return Headers;
            string[] hdrs = hdr.Split('|');
            foreach (string pair in hdrs)
            {
                if (pair.Length == 0) continue;
                string[] nv = pair.Split('=');
                if (nv.Length != 2) throw new Exception("Invalid header string: " + pair);
                Headers[nv[0].Trim()] = nv[1].Trim();
            }
            return Headers;
        }

        /// <summary>
        /// Insert messages to local db tables.
        /// </summary>
        /// <param name="conn">DB connection to be used</param>
        /// <param name="messages">Dictionary mapping table name to a list of messages to be inserted to that table.</param>
        /// <returns></returns>
        protected virtual string InsertMessageBatchToLocalDatabaseQueues(IDbConnection conn, IDictionary<string, ICollection<MessageContainer>> messages)
        {
            string id = null;
            if (messages.Count == 0) return null;
            var tm = Stopwatch.StartNew();
            var allMessages = new List<MessageContainer>();
            try
            {
                using (IDbCommand cmd = conn.CreateCommand())
                {
                    int cnt = 0;
                    cmd.CommandText = "";
                    string prevBody = null;
                    StringWriter sw = null;
                    bool reuseBody = false;
                    string bodyParam = null;
                    foreach (string tableName in messages.Keys)
                    {
                        ICollection<MessageContainer> lmc = messages[tableName];
                        foreach (MessageContainer mw in lmc)
                        {
                            allMessages.Add(mw);
                            System.Diagnostics.Debug.Assert(mw.BodyStr != null);
                            if (prevBody != mw.BodyStr)
                            {
                                prevBody = mw.BodyStr;
                                reuseBody = false;
                                bodyParam = "@msg_body" + cnt;
                            }
                            else reuseBody = true;
                            Dictionary<string, string> headers = RemoveNGHeaders(mw.Headers);


                            string sql = string.Format(@"INSERT INTO {0} with(rowlock) ([from_endpoint], [to_endpoint],[subqueue],[insert_time],[last_processed],[retry_count],[retry_time],[error_info],[msg_text],[correlation_id],[label], [msg_headers], [unique_id])
                                    VALUES
                                    (@from_endpoint{1}, @to_endpoint{1}, @subqueue{1}, getdate(), null, 0, @retry_time{1}, null, {2}, @correl_id{1}, @label{1}, @headers{1}, @unique_id{1});", tableName, cnt, bodyParam);
                            cmd.CommandText += sql + "\n";

                            SqlUtil.AddParameter(cmd, "@from_endpoint" + cnt, mw.From);
                            SqlUtil.AddParameter(cmd, "@to_endpoint" + cnt, mw.To == null ? "" : mw.To);
                            SqlUtil.AddParameter(cmd, "@subqueue" + cnt, mw.IsScheduled ? "R" : "I");
                            SqlUtil.AddParameter(cmd, "@retry_time" + cnt, mw.IsScheduled ? mw.DeliverAt : (mw.HiPriority ? DateTime.Now.AddHours(-24) : DateTime.Now));
                            if (!reuseBody)
                            {
                                SqlUtil.AddParameter(cmd, bodyParam, mw.BodyStr);
                            }
                            SqlUtil.AddParameter(cmd, "@correl_id" + cnt, mw.CorrelationId);
                            string s = mw.ToString();
                            if (string.IsNullOrEmpty(s)) s = mw.Body == null ? "" : mw.Body.ToString();
                            if (s.Length > 100) s = s.Substring(0, 100);

                            SqlUtil.AddParameter(cmd, "@label" + cnt, s);
                            SqlUtil.AddParameter(cmd, "@headers" + cnt, HeadersToString(headers));
                            SqlUtil.AddParameter(cmd, "@unique_id" + cnt, mw.UniqueId);
                            cnt++;
                            if (cmd.Parameters.Count >= MaxSqlParamsInBatch)
                            {
                                cmd.CommandText += "select @@IDENTITY\n";
                                id = Convert.ToString(cmd.ExecuteScalar());
                                cmd.CommandText = "";
                                cmd.Parameters.Clear();
                            }
                        }
                    }
                    if (cmd.CommandText.Length > 0)
                    {
                        cmd.CommandText += "select @@IDENTITY\n";
                        id = Convert.ToString(cmd.ExecuteScalar());
                    }
                }

                tm.Stop();
                log.Log(tm.ElapsedMilliseconds > (500 + allMessages.Count * 10) ? LogLevel.Warn : LogLevel.Trace, "Inserted batch of {0} messages ({1}). Time: {2}", allMessages.Count, id, tm.ElapsedMilliseconds);
                statLog.Info("InsertMessageBatchToQueue:{0}", tm.ElapsedMilliseconds);
                return id;
            }
            catch (Exception ex)
            {
                log.Error("Error inserting message batch: {0}", ex);
                int cnt = 0;
                foreach (MessageContainer mc in allMessages)
                {
                    log.Error("Message: {0}", mc.ToString());
                    if (cnt++ > 5) break;
                }
                throw;
            }
        }

        private void InsertMessageBatchToLocalQueues(ICollection<MessageContainer> messages)
        {
            var cm = _curMsg;
            if (UseReceiveTransactionForSending &&
                CurrentConnection != null)
            {
                log.Debug("Sending batch of {0} messages using the receiving connection", messages.Count);
                InsertMessageBatchToLocalQueues(CurrentConnection, messages);
            }
            else
            {
                using (var con = OpenConnection())
                {
                    InsertMessageBatchToLocalQueues(con, messages);
                }
            }
        }

        /// <summary>
        /// Distributes messages to local database tables.
        /// If the message is destined to a remote database/other location, it will be inserted to the local queue table (Endpoint).
        /// </summary>
        /// <param name="conn"></param>
        /// <param name="messages"></param>
        protected virtual void InsertMessageBatchToLocalQueues(IDbConnection conn, ICollection<MessageContainer> messages)
        {
            if (!SendLocalMessagesDirectly)
            {
                Dictionary<string, ICollection<MessageContainer>> dic = new Dictionary<string, ICollection<MessageContainer>>();
                dic[_queueTable] = messages; //insert all messages to local queue
                InsertMessageBatchToLocalDatabaseQueues(conn, dic);
            }
            else
            {
                //map: connection string -> messages to send
                Dictionary<string, ICollection<MessageContainer>> dic = new Dictionary<string, ICollection<MessageContainer>>();
                var dl = new List<MessageContainer>();
                dic[_queueTable] = dl;
                foreach (MessageContainer mc in messages)
                {
                    string con, tbl;
                    if (SqlUtil.ParseSqlEndpoint(mc.To, out con, out tbl))
                    {
                        var cs = this._connStrings[con];
                        if (con == this._connAlias || SqlUtil.IsSameDatabaseConnection(cs, ConnectionString))
                        {
                            ICollection<MessageContainer> l = null;
                            if (!dic.TryGetValue(tbl, out l))
                            {
                                l = new List<MessageContainer>();
                                dic[tbl] = l;
                            }
                            l.Add(mc);
                            continue;
                        }
                    }
                    dl.Add(mc); //send to local queue.
                }
                InsertMessageBatchToLocalDatabaseQueues(conn, dic);
            }
        }


        /// <summary>
        /// Insert batch of messages to the queue table
        /// </summary>
        /// <param name="connString"></param>
        /// <param name="tableName"></param>
        /// <param name="messages"></param>
        /// <param name="serializer">message serializer to use</param>
        /// <returns>id of last message inserted</returns>
        private string InsertMessageBatchToLocalDatabaseQueues(string connString, IDictionary<string, ICollection<MessageContainer>> messages)
        {
            var cm = _curMsg;
            if (UseReceiveTransactionForSending && 
                CurrentConnection != null && 
                SqlUtil.IsSameDatabaseConnection(CurrentConnection.ConnectionString, connString))
            {
                return InsertMessageBatchToLocalDatabaseQueues(CurrentConnection, messages);
            }
            else
            {
                using (SqlConnection conn = OpenConnection(connString))
                {
                    return InsertMessageBatchToLocalDatabaseQueues(conn, messages);
                }
            }
        }

        /// <summary>
        /// check if specified endpoint is the local queue
        /// </summary>
        /// <param name="endpoint"></param>
        /// <returns></returns>
        public bool IsLocalEndpoint(string endpoint)
        {
            if (string.Equals(Endpoint, endpoint, StringComparison.InvariantCultureIgnoreCase)) return true;
            string remConn, remTable;
            bool b = true;
            if (!SqlUtil.ParseSqlEndpoint(endpoint, out remConn, out remTable)) return false;
            if (!String.Equals(_queueTable, remTable, StringComparison.InvariantCultureIgnoreCase)) return false;
            if (!_connStrings.ContainsKey(remConn)) return false;
            if (!SqlUtil.IsSameDatabaseConnection(ConnectionString, _connStrings[remConn])) return false;
            return true;
        }

        
        

        #region IMessageTransport Members

        [ThreadStatic]
        private static CurMsgInfo _curMsg;

        public MessageContainer CurrentMessage
        {
            get { return _curMsg.Message; }
        }

        public void ProcessCurrentMessageLater(DateTime howLater)
        {
            _curMsg.ProcessLater = howLater;
        }

        #endregion

        [ThreadStatic]
        private static IDbConnection _curCon;
        /// <summary>
        /// Message receiving connection
        /// </summary>
        public static IDbConnection CurrentConnection
        {
            get { return _curCon; }
            private set {_curCon = value;}
        }



        public string Name
        {
            get; set;
        }

        public bool IsEverythingOK
        {
            get 
            {
                return true;
            }
        }

        public string AlertText
        {
            get 
            {
                return null; 
            }
        }

        public DateTime FailingSince
        {
            get 
            { 
                return DateTime.Now; 
            }
        }

        public TimeSpan ProcessingLatency
        {
            get 
            {
                return TimeSpan.FromMilliseconds(this.AverageLatencyMs);
            }
        }

        protected void AccessLocalDb(Action<IDbConnection> act)
        {
            if (CurrentConnection != null)
            {
                act(CurrentConnection);
            }
            else
            {
                using (var conn = OpenConnection())
                {
                    act(conn);
                }
            }
        }

        public void MarkMessageCompleted(string busMessageId)
        {
            AccessLocalDb(con =>
            {
                using (var cmd = con.CreateCommand())
                {
                    cmd.CommandText = string.Format("update {0} set subqueue='X' where id={1}", _queueTable, busMessageId);
                    cmd.ExecuteNonQuery();
                }
            });
        }

        public void MoveToInputQueue(string busMessageId)
        {
            AccessLocalDb(con =>
            {
                using (var cmd = con.CreateCommand())
                {
                    cmd.CommandText = string.Format("update {0} set subqueue='I' where id={1}", _queueTable, busMessageId);
                    cmd.ExecuteNonQuery();
                }
            });
        }

        public void ScheduleMessage(string busMessageId, DateTime deliveryDate)
        {
            AccessLocalDb(con =>
            {
                MarkMessageForProcessingLater(busMessageId, deliveryDate, null, con);
            });
        }
    }
}
