using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NGinnBPM.MessageBus
{
    public interface IMessageHandlerServiceBase
    {
    }

    /// <summary>
    /// Message handler service, used for implementing synchronous service call protocol over
    /// http (see the ServiceClient class). This is completely orthogonal to async message handlers
    /// (IMessageConsumer interface), but sagas can implement both async (IMessageConsumer) and
    /// synchronous (IMessageHandlerService) interfaces and they will get messages from any
    /// of these sources
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface IMessageHandlerService<T> : IMessageHandlerServiceBase
    {
        object Handle(T message);
    }
}
