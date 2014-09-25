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
using System.Text;
using NGinnBPM.MessageBus.Windsor;
using Castle.Windsor;

namespace Tests
{
	/// <summary>
	/// Description of OracleTests.
	/// </summary>
	public class OracleTests
	{
		private static Logger log = LogManager.GetCurrentClassLogger();
		
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
			
			using (Stream stm = typeof(SqlMessageTransport2).Assembly.GetManifestResourceStream("NGinnBPM.MessageBus.createmqueue.oracle.sql"))
            {
				
                AccessOraDb("oradb", con => {
				     Action<string> act = str => {
				        str = string.Format(str, qt);
						try
						{
							using (DbCommand cmd = con.CreateCommand())	
			                {
								cmd.CommandText = str;
			                    cmd.ExecuteNonQuery();
			                }
						}
						catch(Exception ex)
						{
							if (ex.Message.Contains("ORA-00955")) {
								log.Warn("Create script failed: {0}\n{1}", ex.Message, str);
							}
							else {
								log.Warn("Error executing {0}: {1}", str, ex.ToString());
								throw;
							}
						}
				    };
				            	
				    var sr = new StreamReader(stm);
					var sb = new StringBuilder();
					while(!sr.EndOfStream)
					{
						string ln = sr.ReadLine();
						if (ln.Trim() == "--- --- ---")
						{
							act(sb.ToString());
							sb = new StringBuilder();
						}
						else sb.AppendLine(ln);
					}
					if (sb.Length > 0)
					{
						act(sb.ToString());
					}            	
                });
            }
		}
	
		public static IMessageBus ConfigureMessageBus()
		{
			var wc = MessageBusConfigurator.Begin()
				.ConfigureFromAppConfig()
				.UseStaticMessageRouting("route.json")
				.AutoCreateDatabase(false)
				.SetEnableSagas(false)
				.SetAlwaysPublishLocal(true)
				.AddMessageHandlersFromAssembly(typeof(OracleTests).Assembly)
				.SetMaxConcurrentMessages(1)
				.AutoStartMessageBus(true)
				.FinishConfiguration()
				.Container;
				
			return wc.Resolve<IMessageBus>();
		}
		public static void TestSend()
		{
			var mb = ConfigureMessageBus();
			mb.Notify(new TestMessage1 { Id = 11 });
		}
		
		public static void TestNamedQ()
		{
		    var qry = NGinnBPM.MessageBus.Impl.SqlQueue.SqlHelper.GetNamedSqlQuery("CleanupProcessedMessages", "oracle");
		    log.Info(qry);
		}
	}
}
