using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NGinnBPM.MessageBus
{

    public interface ITransactionScope : IDisposable
    {
        void Complete();
    }
    public interface ITransactionScopeFactory
    {
        ITransactionScope CreateTransactionScope();
    }
}
