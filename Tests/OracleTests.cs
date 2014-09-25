/*
 *
 */
using System;
using System.Configuration;
using NLog;
using System.Data.Common;
using NGinnBPM.MessageBus;
using NGinnBPM.MessageBus.Impl;
using System.IO;

namespace Tests
{
	/// <summary>
	/// Description of OracleTests.
	/// </summary>
	public class OracleTests
	{
		public static void AccessOraDb(string csalias, Action<DbConnection> act)
		{
			csalias = csalias ?? "oradb";
			var cs = ConfigurationManager.ConnectionStrings[csalias];
			if (cs == null) throw new Exception(csalias + " - connection string missing");
			Console.WriteLine("Opening {0}", cs.ConnectionString);
			var fac = DbProviderFactories.GetFactory(cs.ProviderName);
			Console.WriteLine("Got factory: {0}", fac.GetType().FullName);
			using (var con = fac.CreateConnection())
			{
				Console.WriteLine("Created connection");
				con.ConnectionString = cs.ConnectionString;
				Console.WriteLine("Opening...");
				con.Open();
				Console.WriteLine("Connection open");
				if (act != null) act(con);
				Console.WriteLine("Closing connection");
			}
		}
		
		public static void TestBasicOps()
		{
			AccessOraDb("oradb", con => {
			            	using (var cmd = con.CreateCommand())
			            	{
			            		cmd.CommandText = "select * from DUAL";
			            		using (var dr = cmd.ExecuteReader())
			            		{
			            			while(dr.Read())
			            			{
			            				Console.WriteLine("{0} = {1}", dr.GetName(0), dr.GetValue(0));
			            			}
			            		}
			            	}
			            });
			            
		}
		
		
		public static void TestDbInit()
		{
			string qt = "mq_test2";
			using (Stream stm = typeof(SqlMessageTransport2).Assembly.GetManifestResourceStream("NGinnBPM.MessageBus.createmqueue.mssql.sql"))
            {
                StreamReader sr = new StreamReader(stm);
                AccessOraDb("oradb", con => {
                    using (DbCommand cmd = con.CreateCommand())
                    {
                        string txt = sr.ReadToEnd();
                        cmd.CommandText = string.Format(txt, qt);
                        cmd.ExecuteNonQuery();
                    }        	
                });
            }
		}
	}
}
