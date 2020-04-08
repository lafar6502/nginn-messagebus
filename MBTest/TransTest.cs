using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NLog;
using System.Transactions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MBTest
{
    [TestClass]
    public class TransTest
    {
        public class TestRM : ISinglePhaseNotification
        {
            public static Logger log = LogManager.GetCurrentClassLogger();

            public bool HadTran = false;
            public string Id { get; set; }
            public void Commit(Enlistment enlistment)
            {
                log.Info("Commit Current trans: {0} {1}", Id, Transaction.Current == null ? "---" : "jest:" + Transaction.Current.TransactionInformation.LocalIdentifier);
                enlistment.Done();
            }

            public void InDoubt(Enlistment enlistment)
            {
                log.Info("Doubt Current trans: {0} {1}", Id, Transaction.Current == null ? "---" : "jest:" + Transaction.Current.TransactionInformation.LocalIdentifier);
            }

            public void Prepare(PreparingEnlistment preparingEnlistment)
            {
                HadTran = Transaction.Current != null;
                log.Info("Prep Current trans: {0} {1}", Id, Transaction.Current == null ? "---" : "jest:" + Transaction.Current.TransactionInformation.LocalIdentifier);
                preparingEnlistment.Done();
            }

            public void Rollback(Enlistment enlistment)
            {
                log.Info("Rollb Current trans: {0} {1}", Id, Transaction.Current == null ? "---" : "jest:" + Transaction.Current.TransactionInformation.LocalIdentifier);
                enlistment.Done();
            }

            public void SinglePhaseCommit(SinglePhaseEnlistment singlePhaseEnlistment)
            {
                log.Info("SPC Current trans: {0} {1}", Id, Transaction.Current == null ? "---" : "jest:" + Transaction.Current.TransactionInformation.LocalIdentifier);
                singlePhaseEnlistment.Done();
            }
        }

        [TestMethod]
        public void TSDisposeTest()
        {
            var rm1 = new TestRM() { Id = "r1" };
            var rm2 = new TestRM() { Id = "r2" };
            using (var ts = new TransactionScope(TransactionScopeOption.Required))
            {
                Transaction.Current.EnlistVolatile(rm1, EnlistmentOptions.EnlistDuringPrepareRequired);
                Transaction.Current.EnlistVolatile(rm2, EnlistmentOptions.EnlistDuringPrepareRequired);
                ts.Complete();
            }
            Assert.IsTrue(rm1.HadTran);
            Assert.IsTrue(rm2.HadTran);
            
            Console.Write("TEST 2 rollback");
            rm1 = new TestRM() { Id = "r1" };
            rm2 = new TestRM() { Id = "r2" };
            using (var ts2 = new TransactionScope(TransactionScopeOption.Required))
            {
                Transaction.Current.EnlistVolatile(rm1, EnlistmentOptions.None);
                Transaction.Current.EnlistVolatile(rm2, EnlistmentOptions.EnlistDuringPrepareRequired);
            }
            Assert.IsFalse(rm1.HadTran);
            Assert.IsTrue(rm2.HadTran);
            Assert.IsTrue(false);
            Console.ReadLine();
        }

        [TestMethod]
        public void ManualTranDisposeTest()
        {
            var rm1 = new TestRM() { Id = "r1" };
            var rm2 = new TestRM() { Id = "r2" };
            using (var tran = new CommittableTransaction())
            {
                Transaction.Current = tran;
                Transaction.Current.EnlistVolatile(rm1, EnlistmentOptions.EnlistDuringPrepareRequired);
                Transaction.Current.EnlistVolatile(rm2, EnlistmentOptions.EnlistDuringPrepareRequired);
                tran.Commit();
                Transaction.Current = null;
            }
            Assert.IsTrue(rm1.HadTran);
            Assert.IsTrue(rm2.HadTran);

            Console.Write("TEST 2 rollback");
            rm1 = new TestRM() { Id = "r1" };
            rm2 = new TestRM() { Id = "r2" };
            using (var tran2 = new CommittableTransaction())
            {
                Transaction.Current = tran2;
                Transaction.Current.EnlistVolatile(rm1, EnlistmentOptions.None);
                Transaction.Current.EnlistVolatile(rm2, EnlistmentOptions.EnlistDuringPrepareRequired);
                tran2.Rollback();
                Transaction.Current = null;
            }
            Assert.IsFalse(rm1.HadTran);
            Assert.IsTrue(rm2.HadTran);
            Assert.IsTrue(false);

        }


    }
}
