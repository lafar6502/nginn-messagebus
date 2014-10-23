using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NGinnBPM.MessageBus.Impl
{
    public enum MessagePreprocessResult
    {
        ContinueProcessing,
        CancelFurtherProcessing
    }
    
    
    /// <summary>
    /// Message preprocessor interface called just after a message has been retrieved from the queue,
    /// but before it's been deserialized and forwarded to its handlers. 
    /// </summary>
    public interface IPreprocessMessages
    {
        /// <summary>
        /// Invoked before message is processed (and even before it is deserialized)
        /// </summary>
        /// <param name="message"></param>
        /// <param name="bus"></param>
        /// <param name="transport"></param>
        /// <returns></returns>
        MessagePreprocessResult HandleIncomingMessage(MessageContainer message, IMessageBus bus, IMessageTransport transport);
        /// <summary>
        /// Invoked after message has been processed (but before transaction commit)
        /// </summary>
        /// <param name="message"></param>
        /// <param name="bus"></param>
        /// <param name="transport"></param>
        /// <param name="ex"></param>
        void AfterMessageProcessed(MessageContainer message, IMessageBus bus, IMessageTransport transport, Exception ex);
    }

}
