
using System;
using System.Data.Common;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Data;
using System.Data.SqlClient;
using NLog;
using System.Text;

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
            para.DbType = DbType.DateTime;
            para.Value = value.HasValue ? new SqlDateTime(value.Value) : SqlDateTime.Null;
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
		
		public void ExecuteDDLBatch(DbConnection con, string query)
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

        public override bool IsSameDatabaseConnection(string connectionString1, string connectionString2)
        {
            return string.Equals(connectionString1, connectionString2, StringComparison.InvariantCultureIgnoreCase);
        }
		
    }
    
    
    
    
}
