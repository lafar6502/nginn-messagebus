using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NGinnBPM.MessageBus;
using Castle.Windsor;
using NLog;
using System.Transactions;
using System.Data;
using System.Data.SqlClient;

namespace Tests
{
    public class PerfTest
    {
        public static void SendTest()
        {
            Dictionary<string, string> connStrings = new Dictionary<string,string>();
            connStrings["testdb1"] = "Data Source=(local);Initial Catalog=NGinn;User Id=nginn;Password=PASS";
            connStrings["testdb2"] = "Data Source=(local);Initial Catalog=NGinn;User Id=nginn;Password=PASS";
            ///configure two containers with two message buses
            IWindsorContainer wc1 = Program.ConfigureMessageBus("sql://testdb1/MQueue1", connStrings, null);

            IMessageBus mb = wc1.Resolve<IMessageBus>();

            var s = "Data Source=(local);Initial Catalog=NGinn;User Id=nginn;Password=PASS";
            //prep(s);
            System.Threading.Thread.Sleep(2000);

            T1(s, mb);
            T2(s, mb);
            T3(s, mb);
            T4(s, mb);
            Console.ReadLine();
        }

        static void T1(string connStr, IMessageBus bus)
        {
            Console.WriteLine("Starting T1");
            DateTime ts = DateTime.Now;
            for (int i = 0; i < 1000; i++)
            {
                using (IDbConnection conn = new SqlConnection(connStr))
                {
                    conn.Open();
                    SendOperation1(bus, conn, i);
                }
            }
            TimeSpan tt = DateTime.Now - ts;
            Console.WriteLine("T1 Time: {0}", tt);
        }

        static void T4(string connStr, IMessageBus bus)
        {
            Console.WriteLine("Starting T4");
            DateTime ts = DateTime.Now;
            for (int i = 0; i < 1000; i++)
            {
                using (IDbConnection conn = new SqlConnection(connStr))
                {
                    MessageBusContext.AppManagedConnection = conn;
                    conn.Open();
                    SendOperation1(bus, conn, i);
                }
            }
            TimeSpan tt = DateTime.Now - ts;
            Console.WriteLine("T4 Time: {0}", tt);
        }

        static void T2(string connStr, IMessageBus bus)
        {
            var to = new TransactionOptions { IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted, Timeout = TimeSpan.FromMinutes(1)};
            Console.WriteLine("Starting T2");
            DateTime ts = DateTime.Now;
            for (int i = 0; i < 1000; i++)
            {
                using (TransactionScope s = new TransactionScope(TransactionScopeOption.Required, to))
                {
                    using (IDbConnection conn = new SqlConnection(connStr))
                    {
                        conn.Open();
                        SendOperation1(bus, conn, i);
                    }
                    s.Complete();
                }
            }
            TimeSpan tt = DateTime.Now - ts;
            Console.WriteLine("T2 Time: {0}", tt);
        }

        static void T3(string connStr, IMessageBus bus)
        {
            var to = new TransactionOptions { IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted, Timeout = TimeSpan.FromMinutes(1) };
            Console.WriteLine("Starting T3");
            DateTime ts = DateTime.Now;
            for (int i = 0; i < 1000; i++)
            {
                using (TransactionScope s = new TransactionScope(TransactionScopeOption.Required, to))
                {
                    using (IDbConnection conn = new SqlConnection(connStr))
                    {
                        MessageBusContext.AppManagedConnection = conn;
                        conn.Open();
                        SendOperation1(bus, conn, i);
                    }
                    s.Complete();
                }
            }
            TimeSpan tt = DateTime.Now - ts;
            Console.WriteLine("T3 Time: {0}", tt);
        }


        public static void prep(string connstr)
        {
            using (IDbConnection con = new SqlConnection(connstr))
            {
                con.Open();
                using (IDbCommand cmd = con.CreateCommand())
                {
                    for (int i = 0; i < 10000; i++)
                    {
                        cmd.CommandText = string.Format("insert into test values({0}, '{1}')", i, DateTime.Now.ToString());
                        cmd.ExecuteNonQuery();
                    }
                }
            }
        }

public static void SendOperation1(IMessageBus bus, IDbConnection conn, int i)
{
    using (IDbCommand cmd = conn.CreateCommand())
    {
        cmd.CommandText = string.Format("select id, name from test where id={0}", i);
        string name;
        using (IDataReader dr = cmd.ExecuteReader())
        {
            dr.Read();
            name = dr.GetString(1);
        }
        cmd.CommandText = string.Format("update Test set name='{1}' where id={0}", i, DateTime.Now.ToString());
        cmd.ExecuteNonQuery();

        bus.Send("sql://testdb1/MQueue2", new TestMessage1 { Id = i });
                
        cmd.CommandText = string.Format("insert into TestLog (tstamp, user_id, summary) values(getdate(), 'I', 'send operation {0}')", i);
        cmd.ExecuteNonQuery();

        bus.Send("sql://testdb1/MQueue2", new TestMessage2 { Text = "first " + i });
        bus.Send("sql://testdb1/MQueue2", new TestMessage2 { Text = "second " + i });
    }
}

    }
}
