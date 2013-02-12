using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.Common;
using Newtonsoft.Json;
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
                            DbInitialize.RunResourceDbScript(con, "NGinnBPM.MessageBus.create_sagatable.sql", new object[] { TableName });
                        });
                    }
                }
                catch (Exception ex)
                {
                    log.Error("Error initializing the saga table {0}: {1}", TableName, ex);
                    throw;
                }
                finally
                {

                }
            }
        }

        protected void AccessDb(Action<DbConnection> act)
        {
            var cn = MessageBusContext.ReceivingConnection as DbConnection;
            if (cn != null && (string.IsNullOrEmpty(ConnectionString) || SqlUtil.IsSameDatabaseConnection(ConnectionString, cn.ConnectionString)))
            {
                act(cn);
            }
            else
            {
                if (string.IsNullOrEmpty(ConnectionString)) throw new Exception("Connection string is not set");
                using (cn = SqlUtil.OpenConnection(ConnectionString, ProviderName))
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
                using (var cmd = con.CreateCommand())
                {
                    cmd.CommandText = string.Format("select data, version from {0} where id=@id", forUpdate ? TableName + " with(updlock) " : TableName);
                    SqlUtil.AddParameter(cmd, "@id", id);
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
                using (var cmd = con.CreateCommand())
                {
                    string newVersion = (Int32.Parse(updatedVersion) + 1).ToString();
                    cmd.CommandText = string.Format("update {0} set data=@data, version=@newVersion, last_updated=@updateDate where id=@id and version=@version", TableName);
                    SqlUtil.AddParameter(cmd, "@id", id);
                    SqlUtil.AddParameter(cmd, "@version", updatedVersion);
                    SqlUtil.AddParameter(cmd, "@newVersion", newVersion);
                    SqlUtil.AddParameter(cmd, "@data", sw.ToString());
                    SqlUtil.AddParameter(cmd, "@updateDate", DateTime.Now);

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
                using (var cmd = con.CreateCommand())
                {
                    cmd.CommandText = string.Format("delete {0} where id=@id", TableName);
                    SqlUtil.AddParameter(cmd, "@id", id);
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
                using (var cmd = con.CreateCommand())
                {
                    cmd.CommandText = string.Format("insert into {0}(id, data, version, created_date, last_updated) values(@id, @data, @version, @updDate, @updDate)", TableName);
                    SqlUtil.AddParameter(cmd, "@id", id);
                    SqlUtil.AddParameter(cmd, "@version", "1");
                    SqlUtil.AddParameter(cmd, "@data", sw.ToString());
                    SqlUtil.AddParameter(cmd, "@updDate", DateTime.Now);
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
