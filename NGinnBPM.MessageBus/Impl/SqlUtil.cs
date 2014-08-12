using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.Data.SqlTypes;
using System.Data.SqlClient;
using System.Data.Common;

namespace NGinnBPM.MessageBus.Impl
{
    public class SqlUtil
    {
        #region SQL
        /// <summary>
        /// Add db command parameter
        /// </summary>
        /// <param name="cmd"></param>
        /// <param name="name"></param>
        /// <param name="value"></param>
        public static void AddParameter(IDbCommand cmd, string name, string value)
        {
            IDbDataParameter prm = cmd.CreateParameter();
            prm.ParameterName = name;
            prm.Value = value == null ? SqlString.Null : value;
            prm.DbType = DbType.AnsiString;
            cmd.Parameters.Add(prm);
        }

        public static void AddParameter(IDbCommand cmd, string name, DateTime? value)
        {
            IDbDataParameter prm = cmd.CreateParameter();
            prm.ParameterName = name;
            prm.Value = value.HasValue ? new SqlDateTime(value.Value) : SqlDateTime.Null;
            prm.DbType = DbType.DateTime;
            cmd.Parameters.Add(prm);
        }

        public static void AddParameter(IDbCommand cmd, string name, long val)
        {
            IDbDataParameter prm = cmd.CreateParameter();
            prm.ParameterName = name;
            prm.Value = val;
            prm.DbType = DbType.Int64;
            cmd.Parameters.Add(prm);
        }

        public static void AddParameter(IDbCommand cmd, string name, int? value)
        {
            IDataParameter para = cmd.CreateParameter();
            para.DbType = DbType.Int32;
            para.Value = value.HasValue ? new System.Data.SqlTypes.SqlInt32(value.Value) : new System.Data.SqlTypes.SqlInt32();
            para.Direction = ParameterDirection.Input;
            para.ParameterName = name;
            cmd.Parameters.Add(para);
        }

        public static void AddParameter(IDbCommand cmd, string name, object val, DbType paramType)
        {
            IDbDataParameter prm = cmd.CreateParameter();
            prm.ParameterName = name;
            prm.Value = val;
            prm.DbType = paramType;
            cmd.Parameters.Add(prm);
        }

        public static void AddParameter(IDbCommand cmd, string name, byte[] value)
        {
            IDbDataParameter prm = cmd.CreateParameter();
            prm.ParameterName = name;
            prm.Value = value;
            prm.DbType = DbType.Binary;
            cmd.Parameters.Add(prm);
        }

        #endregion

        public static bool IsSameDatabaseConnection(string connectionString1, string connectionString2)
        {
            if (string.Equals(connectionString1, connectionString2, StringComparison.InvariantCultureIgnoreCase)) return true;
            SqlConnectionStringBuilder cs1 = new SqlConnectionStringBuilder(connectionString1);
            SqlConnectionStringBuilder cs2 = new SqlConnectionStringBuilder(connectionString2);
            if (!string.Equals(cs1.DataSource, cs2.DataSource, StringComparison.InvariantCultureIgnoreCase)) return false;
            if (!string.Equals(cs1.InitialCatalog, cs2.InitialCatalog, StringComparison.InvariantCultureIgnoreCase)) return false;
            if (cs1.IntegratedSecurity !=  cs2.IntegratedSecurity) return false;
            if (!string.Equals(cs1.UserID, cs2.UserID, StringComparison.InvariantCultureIgnoreCase)) return false;
            //if (!string.Equals(cs1.DataSource, cs2.DataSource, StringComparison.InvariantCultureIgnoreCase) return false;
            return true;
        }

        public static DbConnection OpenConnection(string connectionString, string provider)
        {
        	if (string.IsNullOrEmpty(provider))
        	{
        		var cs = System.Configuration.ConfigurationManager.ConnectionStrings[connectionString];
        		if (cs != null) {
        			provider = cs.ProviderName;
        			connectionString = cs.ConnectionString;
        		}
        	}
        	
            if (string.IsNullOrEmpty(provider)) provider = "System.Data.SqlClient";
            var con = DbProviderFactories.GetFactory(provider).CreateConnection();
            con.ConnectionString = connectionString;
            con.Open();
            return con;
        }

        /// <summary>
        /// Parse the queue endpoint
        /// </summary>
        /// <param name="endpoint"></param>
        /// <param name="connection"></param>
        /// <param name="qTable"></param>
        /// <param name="endpoint"></param>
        /// <returns></returns>
        public static bool ParseSqlEndpoint(string endpoint, out string connection, out string qTable)
        {
            connection = null;
            qTable = null;
            if (!endpoint.StartsWith("sql://"))
                return false;
            endpoint = endpoint.Substring(6);
            string[] ss = endpoint.Split('/');
            if (ss.Length != 2)
                return false;
            connection = ss[0];
            qTable = ss[1];
            return true;
        }

    }
}
