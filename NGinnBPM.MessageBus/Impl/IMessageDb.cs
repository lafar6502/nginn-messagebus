using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NGinnBPM.MessageBus.Impl
{
    public class MessageDao
    {
        public string Id { get; set; }
        public string From { get; set; }
        public string To { get; set; }
        public DateTime InsertTime { get; set; }
        public DateTime RetryTime { get; set; }
        public char Subqueue { get; set; }
        public string BodyText { get; set; }
        public int RetryCount { get; set; }
        public string UniqueId { get; set; }
        public string ErrorInfo { get; set; }
        public DateTime LastProcessed { get; set; }
        public DateTime? TTL { get; set; }
        public string SequenceId { get; set; }
        public int? SequenceNumber { get; set; }
        public int? SequenceLength { get; set; }

        public Dictionary<string, string> Headers { get; set; }

        public bool HasHeader(string name)
        {
            return false;
        }

        public DateTime GetDateTimeHeader(string name, DateTime defValue)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Message database abstraction...
    /// </summary>
    public interface IMessageDb
    {
        void InsertBatch(object dbConnection, IEnumerable<MessageDao> messages);
        MessageDao SelectAndLockNextMessage(object dbConnection);
        void MarkMessageForRetry(object dbConnection, string messageId, DateTime retryDate, string errorInfo);
        void MarkMessageFailed(object dbConnection, string messageId, string errorInfo);
        void MarkMessageCompleted(object dbConnection, string messageId);
        void MoveWaitingMessagesToInputQueue(object dbConnection, IEnumerable<string> messageIds);
    }
}
