using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NGinnBPM.MessageBus
{
    /// <summary>
    /// Throw this exception in the message handler if you don't want the message
    /// to be retried later. This is for situations when the error is permanent
    /// and will not go away in future. Example: data in message is incorrect.
    /// </summary>
    public class PermanentMessageProcessingException : Exception
    {
        public PermanentMessageProcessingException(string msg) : base(msg)
        {
        }

        public PermanentMessageProcessingException()
            : base()
        {
        }

        public PermanentMessageProcessingException(string msg, Exception ex)
            : base(msg, ex)
        {
        }
    }

    /// <summary>
    /// throw this exception in the message handler if you want the message
    /// to be retried later and current transaction to be rolled back.
    /// In a normal case you don't have to throw the exception - you can use IMessageBus.HandleCurrentMessageLater
    /// to re-schedule the message - but this will commit the receiving transaction and all work done so far.
    /// </summary>
    public class RetryMessageProcessingException : Exception
    {
        public DateTime RetryTime { get; set; }

        public RetryMessageProcessingException(DateTime retryTime)
        {
            RetryTime = retryTime;
        }

        public RetryMessageProcessingException(DateTime retryTime, string info) : base(info)
        {
            RetryTime = retryTime;
        }
    }
}
