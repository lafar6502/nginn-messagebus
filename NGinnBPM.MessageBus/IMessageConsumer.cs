using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NGinnBPM.MessageBus
{
    /// <summary>
    /// Marker interface for message consumers
    /// </summary>
    public interface NGMessageConsumer
    {
    }

    /// <summary>
    /// Message consumer interface for routing messages
    /// to components. Should be implemented by components
    /// that wish to receive messages.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface IMessageConsumer<T> : NGMessageConsumer
    {
        void Handle(T message);
    }


   


}
