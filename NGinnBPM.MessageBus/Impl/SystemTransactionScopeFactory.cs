using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Transactions;

namespace NGinnBPM.MessageBus.Impl
{
    public class SystemTransactionScopeFactory : ITransactionScopeFactory
    {
        private class ScopeWrapper : ITransactionScope
        {
            private TransactionScope _ts;
            public ScopeWrapper(TransactionScope ts)
            {
                _ts = ts;
            }

            public void Complete()
            {
                _ts.Complete();
            }

            public void Dispose()
            {
                _ts.Dispose();
            }
        }
        public ITransactionScope CreateTransactionScope()
        {
            TransactionOptions to = new TransactionOptions { IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted, Timeout = DefaultTransactionTimeout };
            return new ScopeWrapper(new TransactionScope(TransactionScopeOption.Required, to));
        }

        public TimeSpan DefaultTransactionTimeout { get; set; } = TimeSpan.FromMinutes(1);
    }
}
