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
using System.Reflection;
using NGinnBPM.MessageBus.Impl.SqlQueue;
using System.Collections.Concurrent;
using System.Data.Common;
using System.Configuration;


namespace NGinnBPM.MessageBus.Impl
{
    /// <summary>
    /// New version of sql message transport
    /// Makes propert use of transaction scopes
    /// 
    /// 
    /// 
    /// </summary>
    public class SqlMessageTransport2 : IStartableService, IMessageTransport, IHealthCheck
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
            var sc = conn as DbConnection;
            if (sc == null && AllowUseOfApplicationDbConnectionForSending)
            {
                sc = MessageBusContext.AppManagedConnection as DbConnection;
            }

            if (sc != null 
                && SqlHelper.IsSameDatabaseConnection(sc.GetType(), sc.ConnectionString, ConnectionString.ConnectionString)
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
        public event Action<DbConnection> OnDatabaseInit;

        #endregion

        protected Logger log = LogManager.GetCurrentClassLogger();
        protected Logger statLog = LogManager.GetLogger("STAT.SqlMessageTransport2");
        
        private string _connAlias;
        private string _queueTable = "MessageQueue";
        private Dictionary<string, ConnectionStringSettings> _connStrings = new  Dictionary<string, ConnectionStringSettings>();


        public virtual string Endpoint
        {
            get 
            {
            	if (string.IsNullOrEmpty(_connAlias)) return null;
            	return string.Format("sql://{0}/{1}", _connAlias, _queueTable); 
            }
            set 
            {
            	if (value == null)
            	{
            		_connAlias = _queueTable = null;
            	}
            	else
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
        /// DB connnection string configuration, alias names are required
        /// </summary>
        public IEnumerable<ConnectionStringSettings> ConnectionStrings
        {
            get { return _connStrings.Values; }
            set 
            {
                _connStrings.Clear();
                foreach(var cs in value)
                {
                    if (string.IsNullOrEmpty(cs.Name)) continue;
                    _connStrings[cs.Name] = cs;
                }
            }
        }
        
        public string DefaultProviderName { get;set;}
        
        /*public IDictionary ConnectionStringDictionary
        {
            get { return _connStrings; }
            set 
            { 
                _connStrings = new Dictionary<string, string>();
                foreach (string k in value.Keys)
                    _connStrings[k] = (string)value[k];
            }
        }*/

        /// <summary>
        /// Connection string for current endpoint
        /// </summary>
        public ConnectionStringSettings ConnectionString
        {
            get
            {
                return GetConnectionString(_connAlias);
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
        
        private static ISqlQueue GetQueueOps(DbConnection c)
        {
            return SqlHelper.GetQueueOps(SqlHelper.GetDialect(c.GetType()));
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

        private DbConnection OpenConnection(string alias)
        {	
            var cs = GetConnectionString(alias);
            if (cs == null) throw new Exception("connection string unknown: " + alias);
            return SqlHelper.OpenConnection(cs);
        }
        
        private DbConnection OpenConnection()
        {	
            return OpenConnection(_connAlias);
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
                using (var con = OpenConnection())
                {
                	return GetQueueOps(con).GetSubqeueSize(con, _queueTable, "R");
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
                using (var con = OpenConnection())
                {
                	return GetQueueOps(con).GetSubqeueSize(con, _queueTable, "F");
                }
            }
        }

        public long AverageLatencyMs
        {
            get
            {
                using (var con = OpenConnection())
                {
                	return GetQueueOps(con).GetAverageLatencyMs(con, _queueTable);
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
                using (var con = OpenConnection())
                {
                	return GetQueueOps(con).GetSubqeueSize(con, _queueTable, "I");
                }
            }
        }
        /// <summary>
        /// Move failed messages back to input subqueue
        /// </summary>
        public void RetryFailedMessages()
        {
            using (var conn = OpenConnection())
            {
            	GetQueueOps(conn).RetryAllFailedMessages(conn, _queueTable);
            }
        }

        
        /// <summary>
        /// Create the message queue table
        /// </summary>
        protected virtual void InitializeQueueTableIfDoesntExist(DbConnection con)
        {
            try
            {
                SqlHelper.RunDDLFromResource(con, "NGinnBPM.MessageBus.createmqueue.${dialect}.sql", new object[] { _queueTable });
            }
            catch (DbException ex)
            {
                log.Warn("Error initializing queue table: {0}", ex.Message);
            }
        }

        /// <summary>
        /// Start processing incoming messages
        /// </summary>
        public virtual void Start()
        {
        	if (string.IsNullOrEmpty(Endpoint) && !this.SendOnly) throw new Exception("Endpoint not configured");
        	
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
                    using (var dbConn = OpenConnection())
                    {
                        if (AutoCreateQueueTable)
                        {
                            InitializeQueueTableIfDoesntExist(dbConn);
                        }
                        if (OnDatabaseInit != null) OnDatabaseInit(dbConn);
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
        
            foreach (var kv in _nowProcessing)
            {
                if (kv.Value.AddSeconds(120) < DateTime.Now)
                {
                    log.Warn("Message {0} is still being processed since {1}", kv.Key, kv.Value);
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
                    var cn = OpenConnection();
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
                            
                                int fcnt = _frequency.Count;
                                if (fcnt == 0) continue;
                                long st = _frequency.FirstOrDefault();
                                window = _freqSw.ElapsedTicks - st;
                                curFreq = window <= 0 ? MaxReceiveFrequency.Value : ((double)fcnt * Stopwatch.Frequency) / window;
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
 
        public bool UseSqlOutputClause { get; set; }

        private ConcurrentDictionary<string, DateTime> _nowProcessing = new ConcurrentDictionary<string, DateTime>();
        private ConcurrentQueue<long> _frequency = new ConcurrentQueue<long>();
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
        protected virtual bool ProcessNextMessage(DbConnection conn)
        {
            var sw = Stopwatch.StartNew();
            string mtype = null;
            DateTime? retryTime = null;
            string id = null; string lbl = "";
            MessageFailureDisposition doRetry = MessageFailureDisposition.RetryIncrementRetryCount;
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
                        //var mc = UseSqlOutputClause ? SelectNextMessageForProcessing2008(conn, out retryTime) : SelectNextMessageForProcessing(conn, out retryTime, out moreMessages);
                        var mc = GetQueueOps(conn).SelectAndLockNextInputMessage(conn, _queueTable, () => _nowProcessing.Keys, out retryTime, out moreMessages);
                        if (mc == null) return moreMessages;
                        id = mc.BusMessageId;
                        _nowProcessing[id] = DateTime.Now;
                        NLog.MappedDiagnosticsContext.Set("nmbrecvmsg", id);
                        log.Debug("Selected message {0} for processing", id);
                        
                        _frequency.Enqueue(_freqSw.ElapsedTicks);
                        long tmp;
                        while (_frequency.Count > MaxConcurrentMessages && _frequency.TryDequeue(out tmp)) {};
                    

                        retryCount = mc.RetryCount;
                        mc.IsFinalRetry = retryCount >= _retryTimes.Length;
                        doRetry = mc.IsFinalRetry ? MessageFailureDisposition.Fail : MessageFailureDisposition.RetryIncrementRetryCount;
                        
                        nextRetry = doRetry == MessageFailureDisposition.RetryIncrementRetryCount ? DateTime.Now + _retryTimes[retryCount] : (DateTime?)null;

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
                                	GetQueueOps(conn).MarkMessageForProcessingLater(conn, _queueTable, id, md.EstimatedRetry.HasValue ? md.EstimatedRetry.Value : DateTime.Now.AddMinutes(1));
                                    abort = false; //save the transaction
                                    return true;
                                }
                                else if (md.MessageDispositon == SequenceMessageDisposition.ProcessingDisposition.HandleMessage)
                                {
                                    if (!string.IsNullOrEmpty(md.NextMessageId))
                                    {
                                    	GetQueueOps(conn).MoveMessageFromRetryToInput(conn, _queueTable, md.NextMessageId);
                                    }
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
                                	GetQueueOps(conn).MarkMessageForProcessingLater(conn, _queueTable, id, _curMsg.ProcessLater.Value);
                                    //MarkMessageForProcessingLater(id, _curMsg.ProcessLater.Value, null, conn);
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
                            doRetry = MessageFailureDisposition.RetryDontIncrementRetryCount;
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
                                doRetry = MessageFailureDisposition.Fail;
                            }
                            if (MessageFailed != null) MessageFailed(mc, handlingError);
                            if (doRetry == MessageFailureDisposition.Fail)
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
                        if (GetQueueOps(conn).MarkMessageFailed(conn, _queueTable, id, handlingError.ToString(), doRetry, nextRetry.HasValue ? nextRetry.Value : DateTime.Now))
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
                	DateTime tm1;
                	_nowProcessing.TryRemove(id, out tm1);
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

        

        


        


        /// <summary>
        /// Delete old handled messages so the database doesn't grow too large
        /// </summary>
        public virtual void CleanupProcessedMessages()
        {
            try
            {
            	
                DateTime t0 = DateTime.Now;
                DateTime lmt = DateTime.Now - MessageRetentionPeriod;
                using (var conn = OpenConnection())
                {
                	GetQueueOps(conn).CleanupProcessedMessages(conn, _queueTable, lmt);
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
       

        protected ConnectionStringSettings GetConnectionString(string alias)
        {
            ConnectionStringSettings cs;
            if (!_connStrings.TryGetValue(alias, out cs)) cs = ConfigurationManager.ConnectionStrings[alias];
            if (cs != null && string.IsNullOrEmpty(cs.ProviderName)) cs.ProviderName = DefaultProviderName;
            return cs;
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
                var cs = GetConnectionString(alias);
                if (cs == null) throw new Exception("Unknown connection string alias: " + alias);
                InsertMessageBatchToLocalDatabaseQueues(cs, d);
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
            	using (var conn = OpenConnection())
                {
            		GetQueueOps(conn).MoveScheduledMessagesToInputQueue(conn, _queueTable);
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
        protected virtual void InsertMessageBatchToLocalQueues(DbConnection conn, ICollection<MessageContainer> messages)
        {
        	var qops = GetQueueOps(conn);
        	if (!SendLocalMessagesDirectly)
            {
                Dictionary<string, ICollection<MessageContainer>> dic = new Dictionary<string, ICollection<MessageContainer>>();
                dic[_queueTable] = messages; //insert all messages to local queue
                qops.InsertMessageBatchToLocalDatabaseQueues(conn, dic);
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
                        var cs = GetConnectionString(con);
                        if (con == this._connAlias || SqlHelper.IsSameDatabaseConnection(cs.ProviderName, cs.ConnectionString, ConnectionString.ConnectionString))
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
                qops.InsertMessageBatchToLocalDatabaseQueues(conn, dic);
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
        private void InsertMessageBatchToLocalDatabaseQueues(ConnectionStringSettings connString, IDictionary<string, ICollection<MessageContainer>> messages)
        {
            var cm = _curMsg;
            if (UseReceiveTransactionForSending && 
                CurrentConnection != null && 
                SqlHelper.IsSameDatabaseConnection(CurrentConnection.GetType(), CurrentConnection.ConnectionString, connString.ConnectionString))
            {
                GetQueueOps(CurrentConnection).InsertMessageBatchToLocalDatabaseQueues(CurrentConnection, messages);
            }
            else
            {
                using (var conn = SqlHelper.OpenConnection(connString))
                {
                	GetQueueOps(conn).InsertMessageBatchToLocalDatabaseQueues(conn, messages);
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
            var cs = GetConnectionString(remConn);
            if (cs == null) return false;
            if (!SqlHelper.IsSameDatabaseConnection(cs.ProviderName, ConnectionString.ConnectionString, cs.ConnectionString)) return false;
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
        private static DbConnection _curCon;
        /// <summary>
        /// Message receiving connection
        /// </summary>
        public static DbConnection CurrentConnection
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
        
        
    }
}
