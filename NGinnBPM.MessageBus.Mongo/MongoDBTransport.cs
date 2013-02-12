using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NGinnBPM.MessageBus.Impl;
using NGinnBPM.MessageBus;
using NLog;
using MongoDB.Driver;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;
using MongoDB.Driver.Builders;
using System.Threading;


namespace NGinnBPM.MessageBus.Mongo
{
    public class MongoMessageWrapper
    {
        /// <summary>
        /// Message ID
        /// </summary>
        public ObjectId _id { get; set; }
        /// <summary>
        /// Sender endpoint
        /// </summary>
        public string From { get; set; }
        /// <summary>
        /// Destination endpoint
        /// </summary>
        public string To { get; set; }
        /// <summary>
        /// json serialized message body
        /// </summary>
        [BsonElement("P")]
        public string Payload { get; set; }
        /// <summary>
        /// subqueue: I R X F P (processing - temporary state)
        /// </summary>
        [BsonElement("Q")]
        public string SubQueue { get; set; }
        [BsonElement("RT")]
        public DateTime RetryTime { get; set; }
        [BsonElement("IT")]
        public DateTime InsertTime { get; set; }
        [BsonElement("EI")]
        public string ErrorInfo { get; set; }
        /// <summary>
        /// Additional message headers
        /// </summary>
        [BsonElement("H")]
        public Dictionary<string, string> Headers { get; set; }
        [BsonElement("R")]
        public int RetryCount { get; set; }
        [BsonElement("LT")]
        public DateTime LastProcessed { get; set; }

        public static IEnumerable<string> GetSkipHeaderNames()
        {
            return new string[] {
                MessageContainer.HDR_RetryCount,
                MessageContainer.HDR_DeliverAt,
                MessageContainer.HDR_BusMessageId
            };
        }
       
    }
    /// <summary>
    /// MongoDB-based message transport
    /// </summary>
    public class MongoDBTransport : IMessageTransport, IStartableService, IHealthCheck
    {
        /// <summary>
        /// Message lock hold time. This is the maximum time lock is held on a message
        /// After that time message will be returned to the queue for processing
        /// </summary>
        public int MessageLockTimeSec { get; set; }
        public TimeSpan MessageRetentionPeriod { get; set; }
        public int MaxConcurrentMessages { get; set; }

        public Dictionary<string, string> ConnectionStrings { get; set; }
        protected Logger log = LogManager.GetCurrentClassLogger();
        protected string _collectionName;
        protected string _connStringAlias;
        protected List<Thread> _procThreads = null;
        private EventWaitHandle _waiter = new AutoResetEvent(true);
        private Timer _maintenanceTimer = null;
        private HashSet<ObjectId> _currentlyProcessed = new HashSet<ObjectId>();

        public MongoDBTransport()
        {
            MessageLockTimeSec = 300; //5 minutes
            MessageRetentionPeriod = TimeSpan.FromDays(7);
            MaxConcurrentMessages = 1;
        }

        /// <summary>
        /// MongoDB endpoint
        /// mongo://connstr/CollectionName
        /// </summary>
        public string Endpoint
        {
            get { return Util.FormatMongoEndpoint(_connStringAlias, _collectionName); }
            set 
            {
                string x, y;
                if (value == null || !Util.ParseMongoEndpoint(value, out x, out y))
                    throw new Exception("Invalid endpoint");
                _collectionName = y;
                _connStringAlias = x;
            }
        }

        public void Send(MessageContainer message)
        {
            if (string.IsNullOrEmpty(message.BodyStr)) throw new Exception("BodyStr");
            var db = OpenDatabase(_connStringAlias);
            db.GetCollection(_collectionName).Insert(Wrap(message));
            _waiter.Set();
        }

        protected void SendToMongoDatabase(string endpoint, MongoMessageWrapper mw)
        {
            throw new NotImplementedException();
        }

        protected  MongoMessageWrapper Wrap(MessageContainer mc)
        {
            var mw = new MongoMessageWrapper
            {
                From = string.IsNullOrEmpty(mc.From) ? this.Endpoint : mc.From,
                To = mc.To,
                InsertTime = DateTime.Now,
                RetryCount = 0,
                RetryTime = mc.DeliverAt,
                SubQueue = "I",
                Payload = mc.BodyStr
            };
            var hl = MongoMessageWrapper.GetSkipHeaderNames();
            if (mc.Headers != null && mc.Headers.Any(x => !hl.Contains(x.Key)))
            {
                mw.Headers = new Dictionary<string, string>();
                foreach (string k in mc.Headers.Keys)
                {
                    if (!hl.Contains(k)) mw.Headers[k] = mc.Headers[k];
                }
            }
            
            return mw;
        }

        /// <summary>
        /// Returns mongodb connection string for given endpoint name
        /// </summary>
        /// <param name="ep"></param>
        /// <returns></returns>
        public string GetMongoConnectionStringForEndpoint(string ep)
        {
            return Util.GetMongoConnectionStringForEndpoint(ep, this.ConnectionStrings);
        }

        protected MessageContainer Unwrap(MongoMessageWrapper m)
        {
            var mc = new MessageContainer();
            mc.Headers = new Dictionary<string, string>();
            if (m.Headers != null)
            {
                foreach (string s in m.Headers.Keys)
                {
                    mc.SetHeader(s, m.Headers[s]);
                }
            }
            
            mc.BodyStr = m.Payload;
            mc.BusMessageId = m._id.ToString();
            mc.RetryCount = m.RetryCount;
            mc.From = m.From;
            mc.To = m.To;
            
            return mc;
        }

        public void SendBatch(IList<MessageContainer> messages, object conn)
        {
            var lst = messages.Select(x => { 
                if (string.IsNullOrEmpty(x.BodyStr)) throw new Exception("BodyStr"); 
                return Wrap(x); 
            });
            OpenDatabase(_connStringAlias).GetCollection(_collectionName).InsertBatch(lst);
        }

        public event MessageArrived OnMessageArrived;

        public event MessageArrived OnMessageToUnknownDestination;

        public MessageContainer CurrentMessage
        {
            get { throw new NotImplementedException(); }
        }

        public void ProcessCurrentMessageLater(DateTime howLater)
        {
            var cm = CurrentMessage;
            if (cm == null) throw new Exception();
            var db = OpenDatabase(_connStringAlias);
            db.GetCollection(_collectionName).Update(Query.EQ("_id", cm.BusMessageId), Update.Set("Q", "R").Set("RT", howLater));
        }

        private Dictionary<string, MongoDatabase> _dbcache = new Dictionary<string, MongoDatabase>();
        protected MongoDatabase OpenDatabase(string connStringAlias)
        {
            MongoDatabase db;
            if (_dbcache.TryGetValue(connStringAlias, out db) && db != null) return db;
            string cstr = GetMongoConnectionStringForEndpoint(connStringAlias);
            if (!cstr.StartsWith(Util.MongoPrefix)) throw new Exception("Invalid connection string: " + cstr);
            db = MongoDatabase.Create(connStringAlias);
            lock (_dbcache)
            {
                _dbcache.Remove(connStringAlias);
                _dbcache[connStringAlias] = db;
                _dbcache.Remove(cstr);
                _dbcache[cstr] = db;
            }
            return db;
        }

        public void Start()
        {
            log.Info("Starting mongodb transport for endpoint {0}", Endpoint);
            if (string.IsNullOrEmpty(_connStringAlias) || string.IsNullOrEmpty(_collectionName))
                throw new Exception("Endpoint incorrect or missing");
            var db = OpenDatabase(_connStringAlias);
            if (!db.CollectionExists(_collectionName))
            {
                log.Info("Creating queue collection {0}", _collectionName);
                db.CreateCollection(_collectionName);
            }
            db.GetCollection(_collectionName).EnsureIndex("Q", "RT");
            _maintenanceTimer = new Timer(new TimerCallback(MaintenanceTask), null, 10000, 30010);

            var thrds = new List<Thread>();
            for (int i = 0; i < MaxConcurrentMessages; i++)
            {
                var th = new Thread(new ThreadStart(ProcessingThreadLoop));
                thrds.Add(th);
                th.Start();
                log.Info("Created processor thread {0}", th.ManagedThreadId);
            }
            _procThreads = thrds;
        }

        protected volatile bool _stop;
        
        public void Stop()
        {
            _stop = true;
            _waiter.Set();
            if (_maintenanceTimer != null)
            {
                _maintenanceTimer.Dispose();
                _maintenanceTimer = null;
            }
            if (_procThreads != null)
            {
                foreach (var t in _procThreads)
                {
                    t.Interrupt();
                }
                foreach (var t in _procThreads)
                {
                    log.Debug("Waiting for thread {0}", t.ManagedThreadId);
                    t.Join();
                }
            }
            _procThreads = null;
            _dbcache = new Dictionary<string, MongoDatabase>();
        }

        public bool IsRunning
        {
            get { throw new NotImplementedException(); }
        }

        private Random _rand = new Random(DateTime.Now.Millisecond);
        protected virtual void ProcessingThreadLoop()
        {
            Thread.Sleep(1000);
            log.Debug("Processing thread started");
            while (!_stop)
            {
                try
                {
                    var db = OpenDatabase(_connStringAlias);
                    bool b = ProcessNextMessage();
                    if (!b)
                    {
                        _waiter.WaitOne(2977 * MaxConcurrentMessages + _rand.Next(1000));
                    }
                }
                catch (ThreadInterruptedException ex)
                {
                }
                catch (ThreadAbortException ex)
                {
                }
                catch (Exception ex)
                {
                    log.Error("Error processing message: {0}", ex);
                    Thread.Sleep(15000);
                }
            }
        }

        private int _inMaintenance = 0;
        private DateTime _lastCleanup = DateTime.MinValue;

        protected virtual void MaintenanceTask(object st)
        {
            if (_stop) return;
            var inm = Interlocked.CompareExchange(ref _inMaintenance, 1, 0);
            if (inm != 0) return;
                
            try
            {
                log.Debug("Maintenance task start");
                var db = OpenDatabase(GetMongoConnectionStringForEndpoint(Endpoint));

                var col = db.GetCollection(_collectionName);
                var cp = GetCurrentlyProcessed();
                if (cp.Length > 0)
                {
                    //touch currently locked messages 
                    col.Update(Query.And(Query.In("_id", BsonArray.Create(cp)), Query.EQ("Q", "P")), Update.Set("LT", DateTime.Now));
                }
                //move from retry to the input queue
                var r = col.Update(
                    Query.And(Query.EQ("Q", "R"), Query.LTE("RT", DateTime.Now)),
                    Update.Set("Q", "I"), SafeMode.True);
                if (r != null && r.DocumentsAffected > 0)
                {
                    log.Info("Moved {0} messages to input queue", r.DocumentsAffected);
                    _waiter.Set();
                }
                if ((DateTime.Now - _lastCleanup).TotalSeconds > 60)
                {
                    _lastCleanup = DateTime.Now;
                    //return abandonned messages to the queue
                    r = col.Update(
                        Query.And(Query.EQ("Q", "P"), Query.LT("LT", DateTime.Now.AddSeconds(-MessageLockTimeSec))),
                        Update.Set("Q", "I"), SafeMode.True);
                    if (r != null && r.DocumentsAffected > 0)
                    {
                        log.Info("Unlocked {0} messages", r.DocumentsAffected);
                        _waiter.Set();
                    }
                    if (MessageRetentionPeriod > TimeSpan.Zero)
                    {
                        col.Remove(Query.And(Query.EQ("Q", "X"), Query.LT("RT", DateTime.Now - MessageRetentionPeriod)));
                    }
                }

                log.Debug("Maintenance task end");
            }
            catch (ThreadInterruptedException)
            {
            }
            catch (Exception ex)
            {
                log.Error("Maintenance task error: {0}", ex);
            }
            finally
            {
                Interlocked.Decrement(ref _inMaintenance);
            }
        }
        /// <summary>
        /// Checks if given endpoint points to the same queue as local database
        /// </summary>
        /// <param name="ep"></param>
        /// <param name="localDb"></param>
        /// <returns></returns>
        protected virtual bool IsLocalEndpoint(string ep, MongoDatabase localDb)
        {
            if (string.Equals(ep, Endpoint)) return true;
            return false;
        }
        
        
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        protected virtual bool ProcessNextMessage()
        {
            var db = OpenDatabase(GetMongoConnectionStringForEndpoint(Endpoint));
            var col = db.GetCollection<MongoMessageWrapper>(_collectionName);

            var m = col.FindAndModify(
                Query.EQ("Q", "I"),
                SortBy.Ascending("RT"),
                Update.Set("Q", "P").Set("LT", DateTime.Now));
            if (!m.Ok) throw new Exception(m.ErrorMessage);
            if (m.ModifiedDocument == null)
            {
                log.Trace("No message selected");
                return false;
            }
            var id = m.ModifiedDocument["_id"].AsObjectId;
            log.Debug("Selected message: {0}", id);
            AddCurrentlyProcessed(id);
            
            try
            {
                var mw = m.GetModifiedDocumentAs<MongoMessageWrapper>();
                var mc = Unwrap(mw);
                bool handled = false;
                if (mc.HasHeader(MessageContainer.HDR_TTL))
                {
                    var dt = mc.GetDateTimeHeader(MessageContainer.HDR_TTL, DateTime.MaxValue);
                    if (dt < DateTime.Now) handled = true;
                }

                try
                {
                    if (!IsLocalEndpoint(mc.To, db))
                    {
                        if (Util.IsValidMongoEndpoint(mc.To))
                        {
                            throw new NotImplementedException();
                        }
                        else
                        {
                            if (OnMessageToUnknownDestination != null)
                            {
                                OnMessageToUnknownDestination(mc, this);
                            }
                            else
                            {
                                throw new PermanentMessageProcessingException("Unknown message destination: " + mc.To);
                            }
                        }
                    }
                    else
                    {
                        if (OnMessageArrived != null && !handled)
                        {
                            OnMessageArrived(mc, this);
                        }
                    }
                    
                    if (MessageRetentionPeriod > TimeSpan.Zero)
                    {
                        col.Update(Query.EQ("_id", id), Update.Set("Q", "X").Set("LT", DateTime.Now));
                    }
                    else
                    {
                        col.Remove(Query.EQ("_id", id));
                    }
                }
                catch (NGinnBPM.MessageBus.RetryMessageProcessingException re)
                {
                    col.Update(Query.EQ("_id", id), Update.Set("Q", "R").Set("RT", re.RetryTime));
                }
                catch (NGinnBPM.MessageBus.PermanentMessageProcessingException e)
                {
                    col.Update(Query.EQ("_id", id), Update.Set("Q", "F").Set("EI", e.InnerException == null ? e.ToString() : e.InnerException.ToString()));
                    //fail it immediately
                }
                catch (Exception ex)
                {
                    log.Debug("Error processing message {0}: {1}", id, ex);
                    int maxRetry = 10;
                    DateTime rt = DateTime.Now;
                    string sq = "R";
                    if (mw.RetryCount < maxRetry)
                    {
                        rt = DateTime.Now.AddMinutes(30);
                    }
                    else
                    {
                        sq = "F";
                    }
                    col.Update(Query.EQ("_id", mw._id), Update.Set("Q", sq).Set("R", mw.RetryCount + 1).Set("EI", ex.InnerException == null ? ex.ToString() : ex.InnerException.ToString()));

                    //retry the message
                }
            }
            finally
            {
                RemoveCurrentlyProcessed(id);
            }
            return true;
        }

        

        public string Name
        {
            get { throw new NotImplementedException(); }
        }

        public bool IsEverythingOK
        {
            get { throw new NotImplementedException(); }
        }

        public string AlertText
        {
            get { throw new NotImplementedException(); }
        }

        public DateTime FailingSince
        {
            get { throw new NotImplementedException(); }
        }

        public TimeSpan ProcessingLatency
        {
            get { throw new NotImplementedException(); }
        }

        protected void AddCurrentlyProcessed(ObjectId id)
        {
            lock (_currentlyProcessed)
            {
                _currentlyProcessed.Add(id);
            }
        }

        protected void RemoveCurrentlyProcessed(ObjectId id)
        {
            lock (_currentlyProcessed)
            {
                _currentlyProcessed.Remove(id);
            }
        }

        protected ObjectId[] GetCurrentlyProcessed()
        {
            lock (_currentlyProcessed)
            {
                return _currentlyProcessed.ToArray();
            }
        }
    }
}
