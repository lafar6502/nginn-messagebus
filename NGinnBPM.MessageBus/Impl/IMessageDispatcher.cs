using System;
using NGinnBPM.MessageBus;

namespace NGinnBPM.MessageBus.Impl
{
    public interface IMessageDispatcher
    {
        /// <summary>
        /// Delier message to all handlers synchronously
        /// </summary>
        /// <param name="message"></param>
        /// <param name="bus"></param>
        void DispatchMessage(object message, IMessageBus bus);
        /// <summary>
        /// Check if there are any handlers for specified message type
        /// </summary>
        /// <param name="messageType"></param>
        /// <returns></returns>
        bool HasHandlerFor(Type messageType);
    }
}
