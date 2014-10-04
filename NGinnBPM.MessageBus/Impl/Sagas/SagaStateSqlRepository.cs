using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.Common;
using Newtonsoft.Json;
using NGinnBPM.MessageBus.Impl.SqlQueue;
using System.IO;
using NLog;

namespace NGinnBPM.MessageBus.Impl.Sagas
{
    /// <summary>
    /// 
    /// </summary>
    public class SqlSagaStateRepository : ISagaRepository, System.ComponentModel.ISupportInitialize
    {
        public string TableName { get; set; }
        public string ConnectionString { get; set; }
        public string ProviderName { get; set; }
        public bool AutoCreateDatabase { get; set; }

        private bool _inited = false;
        private JsonSerializer _ser = new JsonSerializer { TypeNameHandling = TypeNameHandling.None, DefaultValueHandling = DefaultValueHandling.Ignore };
        private Logger log = LogManager.GetCurrentClassLogger();

        public SqlSagaStateRepository()
        {
            AutoCreateDatabase = true;
        }

        private void InitializeIfNeeded()
        {
            lock (this)
            {
                if (_inited) return;
                if (string.IsNullOrEmpty(ProviderName)) ProviderName = "System.Data.SqlClient";
                
                try
                {
                    _inited = true;
                    if (AutoCreateDatabase)
                    {
                        log.Info("Initializing saga table: {0}", TableName);
                        AccessDb(delegate(DbConnection con)
                        {
                            SqlHelper.RunDDLFromResource(con, "NGinnBPM.MessageBus.create_sagatable.${dialect}.sql", new object[] { TableName });
                        });
                    }
                }
                catch (Exception ex)
                {
                    log.Warn("Error initializing the saga table {0}: {1}", TableName, ex.Message);
                }
                finally
                {

                }
            }
        }

        protected void AccessDb(Action<DbConnection> act)
        {
            var cn = MessageBusContext.ReceivingConnection as DbConnection;
            var cs = SqlHelper.GetConnectionString(this.ConnectionString, this.ProviderName);
            if (cn != null && (cs == null || SqlHelper.IsSameDatabaseConnection(cn.GetType(), cs.ConnectionString, cn.ConnectionString)))
            {
                act(cn);
            }
            else
            {
                if (cs == null) throw new Exception("Connection string is not set");
                using (cn = SqlHelper.OpenConnection(cs))
                {
                    act(cn);
                }
            }
        }

        public bool Get(string id, Type stateType, bool forUpdate, out object state, out string version)
        {
            bool ret = false;
            string s = null, v = null;
            AccessDb(delegate(DbConnection con)
            {
                var sq = SqlHelper.GetSqlAbstraction(con);
                using (var cmd = con.CreateCommand())
                {
                    cmd.CommandText = string.Format("select data, version from {0} where id=@id", TableName);
                    if (forUpdate) cmd.CommandText = string.Format(SqlHelper.GetNamedSqlQuery("SqlSagaStateRepository_SelectWithLock", sq.Dialect), TableName);
                    sq.AddParameter(cmd, "id", id);

                    using (var dr = cmd.ExecuteReader())
                    {
                        if (!dr.Read()) return;
                        s = dr.GetString(0);
                        v = dr.GetString(1);
                        ret = true;
                    }
                }
            });

            state = ret ? _ser.Deserialize(new StringReader(s), stateType) : null;
            version = v;
            return ret;
        }

        public void Update(string id, object state, string updatedVersion)
        {
            var sw = new StringWriter();
            _ser.Serialize(sw, state);
            AccessDb(delegate(DbConnection con)
            {
                var sq = SqlHelper.GetSqlAbstraction(con);
                using (var cmd = con.CreateCommand())
                {
                    string newVersion = (Int32.Parse(updatedVersion) + 1).ToString();
                    cmd.CommandText = string.Format(SqlHelper.GetNamedSqlQuery("SqlSagaStateRepository_UpdateSaga", sq.Dialect), TableName);
                    sq.AddParameter(cmd, "id", id);
                    sq.AddParameter(cmd, "version", updatedVersion);
                    sq.AddParameter(cmd, "newVersion", newVersion);
                    sq.AddParameter(cmd, "data", sw.ToString());
                    sq.AddParameter(cmd, "updateDate", DateTime.Now);

                    if (cmd.ExecuteNonQuery() == 0)
                    {
                        throw new Exception("Version conflict");
                    }
                }
            });
        }

        public void Delete(string id)
        {
            AccessDb(delegate(DbConnection con)
            {
                var sq = SqlHelper.GetSqlAbstraction(con);
                using (var cmd = con.CreateCommand())
                {
                    cmd.CommandText = string.Format(SqlHelper.GetNamedSqlQuery("SqlSagaStateRepository_DeleteSaga", sq.Dialect), TableName);
                    sq.AddParameter(cmd, "id", id);
                    if (cmd.ExecuteNonQuery() == 0)
                    {
                        //
                    }
                }
            });
        }

        public void InsertNew(string id, object state)
        {
            var sw = new StringWriter();
            _ser.Serialize(sw, state);
            AccessDb(delegate(DbConnection con)
            {
                var sq = SqlHelper.GetSqlAbstraction(con);
                using (var cmd = con.CreateCommand())
                {
                    cmd.CommandText = string.Format(SqlHelper.GetNamedSqlQuery("SqlSagaStateRepository_InsertSaga", sq.Dialect) , TableName);
                    sq.AddParameter(cmd, "id", id);
                    sq.AddParameter(cmd, "version", "1");
                    sq.AddParameter(cmd, "data", sw.ToString());
                    sq.AddParameter(cmd, "updDate", DateTime.Now);
                    cmd.ExecuteNonQuery();
                }
            });
        }

        public void BeginInit()
        {
            InitializeIfNeeded();
        }

        public void EndInit()
        {
        }
    }

    
}
