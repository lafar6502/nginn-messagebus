using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NGinnBPM.MessageBus;
using System.Data.SqlClient;
using System.Data.Common;
using System.Data.SqlTypes;
using System.Data;
using NGinnBPM.MessageBus.Impl.SqlQueue;
using NLog;
using System.IO;

namespace NGinnBPM.MessageBus.Impl
{
    /// <summary>
    /// Subscription database 
    /// </summary>
    public class SqlSubscriptionService : ISubscriptionService
    {
        private Logger log = LogManager.GetCurrentClassLogger();

        public SqlSubscriptionService()
        {
            SubscriptionTableName = "NGinnMessageBus_Subscriptions";
            AutoCreateSubscriptionTable = true;
            CacheExpiration = TimeSpan.FromMinutes(60); //1-hour expiration
            DbProvider = "System.Data.SqlClient";
        }

        public string ConnectionString { get; set; }
        public string DbProvider { get; set; }

        public string SubscriptionTableName { get; set; }
        public string Endpoint { get; set; }
        public bool AutoCreateSubscriptionTable { get; set; }
        public TimeSpan CacheExpiration { get; set; }
        
        private Dictionary<string, List<string>> _cache = null;
        private DateTime _lastCacheLoad = DateTime.Now;
        private static string[] empty = new string[0];

        #region ISubscriptionService Members

        protected void AccessDb(Action<DbConnection> act)
        {
            var cn = MessageBusContext.ReceivingConnection as DbConnection;
            var cs = SqlHelper.GetConnectionString(ConnectionString, DbProvider);
            if (cn != null 
                && (cs == null || SqlHelper.IsSameDatabaseConnection(cn.GetType(), cn.ConnectionString, cs.ConnectionString))
                && cn.State == ConnectionState.Open)
            {
                act(cn);
            }
            else
            {
                using (var cn2 = SqlHelper.OpenConnection(cs))
                {
                    act(cn2);
                }
            }
        }

        public IEnumerable<string> GetTargetEndpoints(string messageType)
        {
            List<string> lst = null;
            InitializeIfNeeded();
            if (_lastCacheLoad + CacheExpiration < DateTime.Now)
            {
                _cache = null;
            }
            var c = _cache;
            if (c == null)
            {
                AccessDb(delegate(DbConnection con)
                {
                    var sq = SqlHelper.GetSqlAbstraction(con);
                    c = new Dictionary<string, List<string>>();
                    using (DbCommand cmd = con.CreateCommand())
                    {
                        var qry = SqlHelper.GetNamedSqlQuery("SqlSubscriptionService_GetSubscriptions", SqlHelper.GetDialect(con.GetType()));
                        cmd.CommandText = string.Format(qry, SubscriptionTableName);
                        sq.AddParameter(cmd, "pub", Endpoint);

                        using (IDataReader dr = cmd.ExecuteReader())
                        {
                            while (dr.Read())
                            {
                                string mtype = dr.GetString(1), sub = dr.GetString(0);
                                if (!c.TryGetValue(mtype, out lst)) { lst = new List<string>(); c[mtype] = lst; }
                                if (!lst.Contains(sub)) lst.Add(sub);
                            }
                        }
                    }
                });
                lock (this)
                {
                    _cache = c;
                    _lastCacheLoad = DateTime.Now;
                }
            }
            return c.TryGetValue(messageType, out lst) ? lst : (IEnumerable<string>) empty;
        }

        public void Subscribe(string subscriberEndpoint, string messageType, DateTime? expiration)
        {
            InitializeIfNeeded();
            if (expiration.HasValue && expiration.Value < DateTime.Now) return;
            AccessDb(delegate(DbConnection con)
            {
                var sq = SqlHelper.GetSqlAbstraction(con);
                string dialect = SqlHelper.GetDialect(con.GetType());
                using (DbCommand cmd = con.CreateCommand())
                {
                    cmd.CommandText = string.Format(SqlHelper.GetNamedSqlQuery("SqlSubscriptionService_UpdateSubscription", dialect), SubscriptionTableName);
                    sq.AddParameter(cmd, "pub", Endpoint);
                    sq.AddParameter(cmd, "sub", subscriberEndpoint);
                    sq.AddParameter(cmd, "mtype", messageType);
                    sq.AddParameter(cmd, "expiration", expiration);

                    var rows = cmd.ExecuteNonQuery();
                    if (rows == 0)
                    {
                        cmd.CommandText = string.Format(SqlHelper.GetNamedSqlQuery("SqlSubscriptionService_InsertSubscription", dialect), SubscriptionTableName);
                        cmd.Parameters.Clear();
                        sq.AddParameter(cmd, "@pub", Endpoint);
                        sq.AddParameter(cmd, "@sub", subscriberEndpoint);
                        sq.AddParameter(cmd, "@mtype", messageType);
                        sq.AddParameter(cmd, "@expiration", expiration);
                        cmd.ExecuteNonQuery();
                    }
                }
            });
            _cache = null;
        }

        public void Unsubscribe(string subscriberEndpoint, string messageType)
        {
            InitializeIfNeeded();
            AccessDb(delegate(DbConnection con)
            {
                var sq = SqlHelper.GetSqlAbstraction(con);
                string dialect = SqlHelper.GetDialect(con.GetType());
                using (DbCommand cmd = con.CreateCommand())
                {
                    cmd.CommandText = string.Format(SqlHelper.GetNamedSqlQuery("SqlSubscriptionService_DeleteSubscription", dialect), this.SubscriptionTableName);
                    sq.AddParameter(cmd, "pub", Endpoint);
                    sq.AddParameter(cmd, "sub", subscriberEndpoint);
                    sq.AddParameter(cmd, "mtype", messageType);
                    cmd.ExecuteNonQuery();
                }
            });
            _cache = null;
        }

        #endregion

        protected void InitializeSubscriptionTable()
        {

            using (Stream stm = typeof(SqlSubscriptionService).Assembly.GetManifestResourceStream("NGinnBPM.MessageBus.create_subscribertable.mssql.sql"))
            {
                StreamReader sr = new StreamReader(stm);
                AccessDb(delegate(DbConnection con)
                {
                    var sq = SqlHelper.GetSqlAbstraction(con);
                    string txt = sr.ReadToEnd();
                    txt = string.Format(txt, SubscriptionTableName);
                    sq.ExecuteDDLBatch(con, txt);
                });
            }
        }

        private bool _inited = false;
        protected void InitializeIfNeeded()
        {
            bool b = _inited;
            if (b) return;
            lock (this)
            {
                if (_inited) return;
                try
                {
                    if (AutoCreateSubscriptionTable)
                    {
                        InitializeSubscriptionTable();
                    }
                }
                catch (Exception ex)
                {
                    log.Error("Error initializing subscription table: {0}", ex);
                }
                _inited = true;
            }
        }

        

        public void HandleSubscriptionExpirationIfNecessary(string subscriberEndpoint, string messageType)
        {
            InitializeIfNeeded();
            AccessDb(delegate(DbConnection con)
            {
                var sq = SqlHelper.GetSqlAbstraction(con);
                string dialect = SqlHelper.GetDialect(con.GetType());
                using (DbCommand cmd = con.CreateCommand())
                {
                    cmd.CommandText = string.Format(SqlHelper.GetNamedSqlQuery("SqlSubscriptionService_ExpireSubscriptions", dialect), SubscriptionTableName);
                    sq.AddParameter(cmd, "pub", Endpoint);
                    sq.AddParameter(cmd, "sub", subscriberEndpoint);
                    sq.AddParameter(cmd, "mtype", messageType);

                    var rows = cmd.ExecuteNonQuery();
                    if (rows == 0) return;
                    log.Warn("Subscription expired: {0} {1}", subscriberEndpoint, messageType);
                    _cache = null;
                }

            });
        }
    }
}
