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

    /// <summary>
    /// Outgoing message handler allows you to plug your logic into transaction
    /// where the message is initiated. These handlers will be invoked synchronously on sending
    /// the message, in same transaction. This means any exception in a handler will result
    /// in whole transaction being aborted.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface IOutgoingMessageHandler<T> : NGMessageConsumer
    {
        void Handle(T message);
    }

   


}
