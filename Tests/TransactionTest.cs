using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Transactions;
using System.Data;
using System.Data.SqlClient;
using NLog;

namespace Tests
{
    class TransactionTest
    {
        private static Logger log = LogManager.GetCurrentClassLogger();
        
        static TransactionTest()
        {
            System.Transactions.TransactionManager.DistributedTransactionStarted += new TransactionStartedEventHandler(TransactionManager_DistributedTransactionStarted);
        }

        static void TransactionManager_DistributedTransactionStarted(object sender, TransactionEventArgs e)
        {
            log.Warn("\n*******************\nDistributed transaction!\n*********************\n");

        }

        public static void Test3(string connstr)
        {
            using (TransactionScope ts1 = new TransactionScope())
            {
                using (IDbConnection conn = new SqlConnection(connstr))
                {
                    conn.Open();
                    using (IDbCommand cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "update test set name='baba' where id=1";
                        cmd.ExecuteNonQuery();
                    }
                }
                log.Info("Conn 1 closed. Opening conn 2");
                using (IDbConnection conn = new SqlConnection(connstr))
                {
                    conn.Open();
                    log.Info("Conn 2 opened. Should not start a dtc transaction");
                    
                    using (IDbCommand cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "update test set name='baba' where id=1";
                        cmd.ExecuteNonQuery();
                    }

                    using (IDbConnection conn3 = new SqlConnection(connstr))
                    {
                        log.Info("Opening conn 3. This will trigger the promotion.");
                        conn3.Open();
                        using (IDbCommand cmd = conn3.CreateCommand())
                        {
                            cmd.CommandText = "update test set name='baba' where id=1";
                            cmd.ExecuteNonQuery();
                        }
                    }
                    log.Info("Conn 3 closed");
                }
            }
        }
        public static void Test1(string connstr)
        {
            using (TransactionScope ts1 = new TransactionScope())
            {
                
                using (IDbConnection conn = new SqlConnection(connstr))
                {
                    conn.Open();
                    using (IDbCommand cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "insert into test(id, name) values(1, 'ola')";
                        cmd.ExecuteNonQuery();
                    }

                    using (TransactionScope ts2 = new TransactionScope(TransactionScopeOption.Required))
                    {
                        using (IDbCommand cmd = conn.CreateCommand())
                        {
                            cmd.CommandText = "update test set name='ziutek' where id=1";
                            var cnt = cmd.ExecuteNonQuery();
                            log.Info("Updated {0} rows", cnt);
                        }
                    }
                    //nic z tego - transakcja juz zepsuta
                    using (IDbCommand cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "update test set name='bolo' where id=1 and name = 'ziutek'";
                        var cnt = cmd.ExecuteNonQuery();
                        log.Info("2 Updated {0} rows", cnt);
                    }
                }
            }
        }

        public static void Test2(string connstr)
        {
            
            using (var conn = new SqlConnection(connstr))
            {
                conn.Open();
                    
                using (var ts1 = new TransactionScope())
                {
                    conn.EnlistTransaction(Transaction.Current);
                    using (IDbCommand cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "insert into test(id, name) values(1, 'ola')";
                        cmd.ExecuteNonQuery();
                    }
                }
                //now outside

                using (IDbCommand cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "select count(*) from test";
                    log.Info("Rows: {0}", cmd.ExecuteScalar());
                }

                using (var ts2 = new TransactionScope())
                {
                    conn.EnlistTransaction(Transaction.Current);
                    using (IDbCommand cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "insert into test(id, name) values(1, 'ola')";
                        cmd.ExecuteNonQuery();
                    }
                    ts2.Complete();
                }
                //now outside

                using (IDbCommand cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "select count(*) from test";
                    log.Info("Rows: {0}", cmd.ExecuteScalar());
                }
                    
            }
            
        }
    }
}
