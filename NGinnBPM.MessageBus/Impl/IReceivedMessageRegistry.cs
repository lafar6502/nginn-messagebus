using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NGinnBPM.MessageBus.Impl
{
    public interface IReceivedMessageRegistry
    {
        bool HasBeenReceived(string id);
        void RegisterReceived(string id);
    }
}
