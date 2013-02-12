using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using NLog;
using System.Data;
using System.Data.SqlClient;

namespace NGinnBPM.MessageBus.Impl
{
    public class DbInitialize 
    {
        public string ConnectionString { get; set; }
        private static Logger log = LogManager.GetCurrentClassLogger();

        public void RunDbScript(string rcName, object[] formatParams)
        {
            using (var cn = OpenConnection())
            {
                RunResourceDbScript(cn, rcName, formatParams);
            }
        }

        public static void RunResourceDbScript(IDbConnection cn, string rcName, object[] formatParams)
        {
            try
            {
                using (Stream stm = typeof(DbInitialize).Assembly.GetManifestResourceStream(rcName))
                {
                    StreamReader sr = new StreamReader(stm);
                    
                    using (IDbCommand cmd = cn.CreateCommand())
                    {
                        string txt = sr.ReadToEnd();
                        cmd.CommandText = string.Format(txt, formatParams);
                        cmd.ExecuteNonQuery();
                    }
                    log.Debug("Successfully run db script: {0}", rcName);
                }
            }
            catch (Exception ex)
            {
                log.Error("Error running db script {0}: {1}", rcName, ex);
                throw;
            }
        }

        
        protected virtual IDbConnection OpenConnection()
        {
            var cn = new SqlConnection(ConnectionString);
            cn.Open();
            return cn;
        }

        

        

    }
}
