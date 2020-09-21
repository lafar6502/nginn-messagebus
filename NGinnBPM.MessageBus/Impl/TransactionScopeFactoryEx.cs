using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Transactions;

namespace NGinnBPM.MessageBus.Impl
{
    internal class NGTranScope : ITransactionScope
    {
        private CommittableTransaction _tran;
        private Transaction _prevTran;
        private bool _completed = false;
        public NGTranScope(TimeSpan timeout)
        {
            _prevTran = Transaction.Current;
            var to = new TransactionOptions
            {
                IsolationLevel = IsolationLevel.ReadCommitted,
                Timeout = timeout
            };
            _tran = new CommittableTransaction(to);
            Transaction.Current = _tran;
        }

        public void Complete()
        {
            if (_completed) throw new Exception("You can only call Complete once");
            _completed = true;
        }

        public void Dispose()
        {
            if (_tran == null) throw new Exception("Already disposed");
            try
            {
                if (_completed)
                {
                    _tran.Commit();
                }
                else
                {
                    _tran.Rollback();
                }
            }
            finally
            {
                try
                {
                    _tran.Dispose();
                }
                finally
                {
                    _tran = null;
                    Transaction.Current = _prevTran;
                }
            }
        }
    }
    public class TransactionScopeFactoryEx : ITransactionScopeFactory
    {
        public TimeSpan DefaultTransactionTimeout { get; set; } = TimeSpan.FromMinutes(1);

        public ITransactionScope CreateTransactionScope()
        {
            return new NGTranScope(DefaultTransactionTimeout);
        }
    }
}
