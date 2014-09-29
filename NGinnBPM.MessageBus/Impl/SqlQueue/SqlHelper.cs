/*
 *
 */
using System;
using System.Collections;
using System.Data.Common;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.IO;
using System.Text;
using System.Configuration;

namespace NGinnBPM.MessageBus.Impl.SqlQueue
{
	/// <summary>
	/// Description of SqlHelper.
	/// </summary>
	public class SqlHelper
	{
	    private static Dictionary<string, Dictionary<string, string>> _namedQueries = null;
	    
	    private static Dictionary<string, ISqlAbstractions> _abstr = new Dictionary<string, ISqlAbstractions> {
	        {"oracle", new SqlAbstract_oracle() },
	        {"sqlserver", new SqlAbstract_sqlserver() }
	    };
	    
		public static string GetDialect(string dbProviderName)
		{
		    if (dbProviderName.ToLower().Contains("oracle")) return "oracle";
		    return "sqlserver";
		}
		
		public static string GetDialect(Type connectionType)
		{
		    if (connectionType.Name.ToLower().Contains("oracle")) return "oracle";
		    return "sqlserver";
		}
		
		public static ISqlAbstractions GetSqlAbstraction(string dialect)
		{
		    ISqlAbstractions ret;
		    return _abstr.TryGetValue(dialect, out ret) ? ret : null;
		}

        public static ISqlAbstractions GetSqlAbstraction(DbConnection conn)
        {
            return GetSqlAbstraction(GetDialect(conn.GetType()));
        }
		
		protected static Dictionary<string, string> ReadNamedQueryResource(string dialect)
		{
		    using (Stream stm = typeof(SqlHelper).Assembly.GetManifestResourceStream("NGinnBPM.MessageBus.Impl.SqlQueue.NamedQueries." + dialect + ".json"))
            {
		        return JsonConvert.DeserializeObject<Dictionary<string, string>>(new StreamReader(stm, Encoding.UTF8).ReadToEnd());
		    }
		}

        public static string FormatSqlQuery(string queryId, string dialect, params object[] prm)
        {
            var q = GetNamedSqlQuery(queryId, dialect);
            return string.Format(q, prm);
        }

		public static string GetNamedSqlQuery(string queryId, string dialect)
		{
		    var d = _namedQueries;
		    if (d == null)
		    {
		        d = new Dictionary<string, Dictionary<string, string>>();
		        foreach(var dia in new string[] {"oracle", "sqlserver"})
		        {
		            d[dia] = ReadNamedQueryResource(dia);
		        }
		        _namedQueries = d;
		    }
		    Dictionary<string, string> q;
		    if (!d.TryGetValue(dialect, out q)) throw new Exception("No named queries for dialect " + dialect);
		    string qry;
		    if (!q.TryGetValue(queryId, out qry)) throw new Exception("Query not found: " + queryId);
		    return qry;
		}
		
		public static void SetQueryParams(DbCommand cmd, IDictionary<string, object> values)
		{
		    
		}
		
		
		
		public static void SetParam(DbCommand cmd, string name, int? value)
		{
		    string dialect = GetDialect(cmd.Connection.GetType());
		    var abs = GetSqlAbstraction(dialect);
		    abs.AddParameter(cmd, name, value);
		}
		
		public static void SetParam(DbCommand cmd, string name, long? value)
		{
		    string dialect = GetDialect(cmd.Connection.GetType());
		    var abs = GetSqlAbstraction(dialect);
		    abs.AddParameter(cmd, name, value);
		}
		
		public static void SetParam(DbCommand cmd, string name, string value)
		{
		    string dialect = GetDialect(cmd.Connection.GetType());
		    var abs = GetSqlAbstraction(dialect);
		    abs.AddParameter(cmd, name, value);
		}
		
		public static void SetParam(DbCommand cmd, string name, DateTime? value)
		{
		    string dialect = GetDialect(cmd.Connection.GetType());
		    var abs = GetSqlAbstraction(dialect);
		    abs.AddParameter(cmd, name, value);
		}
		
		public static void RunDDL(DbConnection con, string ddlBatch)
		{
		    var di = GetDialect(con.GetType());
		    var abs = GetSqlAbstraction(di);
		    abs.ExecuteDDLBatch(con, ddlBatch);
		}
		
		public static ISqlQueue GetQueueOps(string dialect)
		{
		    if (dialect == "oracle") return new OracleQueueOps();
		    return new CommonQueueOps(dialect);
		}

        public static DbConnection OpenConnection(string connectionString, string dbProvider = null)
        {
            var cs = ConfigurationManager.ConnectionStrings[connectionString];
            if (cs != null)
            {
                dbProvider = cs.ProviderName;
                connectionString = cs.ConnectionString;
            };
            if (dbProvider == null) dbProvider = "System.Data.SqlClient";
            var cn = DbProviderFactories.GetFactory(dbProvider).CreateConnection();
            try
            {
                cn.ConnectionString = connectionString;
                cn.Open();
                return cn;
            }
            catch(Exception)
            {
                cn.Dispose();
                throw; 
            }
        }

        public static DbConnection OpenConnection(ConnectionStringSettings cs)
        {
            return OpenConnection(cs.ConnectionString, cs.ProviderName);
        }
	}
}
