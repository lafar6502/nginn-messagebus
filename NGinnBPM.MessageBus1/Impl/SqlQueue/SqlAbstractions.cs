
using System;
using System.Data.Common;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Data;
using System.Data.SqlClient;
using NLog;
using System.Text;
using System.IO;

namespace NGinnBPM.MessageBus.Impl.SqlQueue
{
    public class SqlAbstract_sqlserver : ISqlAbstractions
    {
        protected static Logger log = LogManager.GetCurrentClassLogger();
        /// <summary>
        /// 'normalize' parameter name
        /// </summary>
        /// <param name="n"></param>
        /// <returns></returns>
		public virtual string NormName(string n)
        {
            return n.StartsWith("@") ? n : "@" + n;
        }
		
		public virtual void AddParameter(DbCommand cmd, string parameterAlias, string value)
		{
			var prm = cmd.CreateParameter();
			prm.ParameterName = NormName(parameterAlias);
            prm.DbType = DbType.String;
            prm.Value = value == null ? SqlString.Null : new SqlString(value);
            cmd.Parameters.Add(prm);
		}
		public virtual void AddParameter(DbCommand cmd, string parameterAlias, int? value)
		{
			IDataParameter para = cmd.CreateParameter();
            para.DbType = DbType.Int32;
            para.Value = value.HasValue ? new SqlInt32(value.Value) : SqlInt32.Null;
            para.Direction = ParameterDirection.Input;
            para.ParameterName = NormName(parameterAlias);
            cmd.Parameters.Add(para);
		}
		public virtual void AddParameter(DbCommand cmd, string parameterAlias, DateTime? value)
		{
			IDataParameter para = cmd.CreateParameter();
            para.DbType = DbType.DateTime2;
            para.Value = value.HasValue ? (object) value.Value : null;
            para.Direction = ParameterDirection.Input;
            para.ParameterName = NormName(parameterAlias);
            cmd.Parameters.Add(para);
		}
		public virtual void AddParameter(DbCommand cmd, string parameterAlias, long? value)
		{
			IDataParameter para = cmd.CreateParameter();
            para.DbType = DbType.Int64;
            para.Value = value.HasValue ? new SqlInt64(value.Value) : SqlInt64.Null;
            para.Direction = ParameterDirection.Input;
            para.ParameterName = NormName(parameterAlias);
            cmd.Parameters.Add(para);
		}
		
		public virtual void ExecuteDDLBatch(DbConnection con, string query)
		{
		    using (var cmd = con.CreateCommand())
		    {
		        cmd.CommandText = query;
		        cmd.ExecuteNonQuery();
		    }
		}


        public static string DumpCommandParams(DbCommand cmd)
        {
            var sb = new StringBuilder();
            foreach (DbParameter p in cmd.Parameters)
            {
                if (sb.Length > 0) sb.Append(", ");
                sb.AppendFormat("{0}={1}", p.ParameterName, p.Value);
            }
            return sb.ToString();
        }


        public virtual string Dialect
        {
            get { return "sqlserver"; }
        }

		public virtual bool IsSameDatabaseConnection(string connectionString1, string connectionString2)
		{
            if (string.Equals(connectionString1, connectionString2, StringComparison.InvariantCultureIgnoreCase)) return true;
            SqlConnectionStringBuilder b1 = new SqlConnectionStringBuilder(connectionString1);
            SqlConnectionStringBuilder b2 = new SqlConnectionStringBuilder(connectionString2);
            if (string.Equals(b1.DataSource, b2.DataSource, StringComparison.InvariantCultureIgnoreCase) &&
                string.Equals(b1.UserID, b2.UserID, StringComparison.InvariantCultureIgnoreCase) &&
                string.Equals(b1.InitialCatalog, b2.InitialCatalog, StringComparison.InvariantCultureIgnoreCase))
            {
                return true;
            }

            return false;

		}


        public virtual DbCommand CreateCommand(DbConnection conn)
        {
            return conn.CreateCommand();
        }


        public virtual bool IsSameDatabaseConnection(DbConnection conn, string connectionString)
        {
            return IsSameDatabaseConnection(conn.ConnectionString, connectionString);
        }


        public virtual int MaxNumberOfQueryParams
        {
            get { return 1000; }
        }
    }
    
    
    
    
    public class SqlAbstract_oracle : SqlAbstract_sqlserver
    {
        public override string NormName(string n)
        {
            return n.StartsWith(":") ? n.Substring(1) : n;
        }
        
        public override void AddParameter(DbCommand cmd, string parameterAlias, DateTime? value)
        {
            var para = cmd.CreateParameter();
            para.DbType = DbType.DateTime;
            para.ParameterName = NormName(parameterAlias);
            para.Direction = ParameterDirection.Input;
            para.Value = value.HasValue ? (object) value.Value : SqlDateTime.Null;
            cmd.Parameters.Add(para);
            log.Info("Added dt param {0}={1}", para.ParameterName, para.Value);
        }
        
        public override void AddParameter(DbCommand cmd, string parameterAlias, long? value)
        {
            var para = cmd.CreateParameter();
            para.DbType = DbType.Int64;
            para.Value = value.HasValue ? value.Value : (object) SqlInt64.Null;
            para.Direction = ParameterDirection.Input;
            para.ParameterName = NormName(parameterAlias);
            cmd.Parameters.Add(para);
        }

        public override string Dialect
        {
            get
            {
                return "oracle";
            }
        }

        

        public static readonly string DDL_Statement_Separator = "--- --- ---";

        public override void ExecuteDDLBatch(DbConnection con, string query)
        {
            var sb = new StringBuilder();
            var sr = new StringReader(query);
            
            using (var cmd = con.CreateCommand())
            {
                Action<string> exec = s =>
                {
                    try
                    {
                        log.Info("** ORA EXECUTING:\n{0}", s);
                        cmd.CommandText = s;
                        cmd.ExecuteNonQuery();
                    }
                    catch (Exception ex)
                    {
                        log.Warn("Error executing {0}: {1}", s, ex.Message);
                        throw;
                    }
                };
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    if (line.Trim() == DDL_Statement_Separator)
                    {
                        exec(sb.ToString());
                        sb = new StringBuilder();
                    }
                    else
                    {
                        sb.AppendLine(line);
                    }
                }
                if (sb.Length > 0)
                {
                    exec(sb.ToString());
                }
            }
        }

        public override DbCommand CreateCommand(DbConnection conn)
        {
            var cmd = conn.CreateCommand();
            var pi = cmd.GetType().GetProperty("BindByName");
            if (pi != null) pi.SetValue(cmd, true, null);
            return cmd;
        }
		
    }
    
    
    
    
}
