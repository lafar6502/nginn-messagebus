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
        MessagePreprocessResult HandleIncomingMessage(MessageContainer message, IMessageTransport transport, out Action<MessageContainer, Exception> afterProcessCallback);
    }

}
