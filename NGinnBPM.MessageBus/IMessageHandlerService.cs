using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NGinnBPM.MessageBus
{
    public interface IMessageHandlerServiceBase
    {
    }

    public interface IMessageHandlerService<T> : IMessageHandlerServiceBase
    {
        object Handle(T message);
    }
}
