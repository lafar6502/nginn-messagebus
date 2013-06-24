using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NGinnBPM.MessageBus.Queues
{
    public interface IQueueOperations
    {
        void MarkMessageCompleted(string busMessageId);
        void MoveToInputQueue(string busMessageId);
        void ScheduleMessage(string busMessageId, DateTime deliveryDate);
    }
    /// <summary>
    /// Queue-as-an-SQL-table abstraction
    /// </summary>
    public class SQLQueue
    {
        public string ConnectionString { get; set; }
        public string TableName { get; set; }

        public class Message
        {
            long Id { get; set; }
            string Payload { get; set; }
            int RetryCount { get; set; }
            DateTime InsertTime { get; set; }
            DateTime RetryTime { get; set; }
            string UniqueId { get; set; }
            string Headers { get; set; }
            string Label { get; set; }
            string From { get; set; }
            string To { get; set; }
        }

        bool ProcessNextMessage(Action<Message, SQLQueue> act)
        {
            throw new NotImplementedException();
        }

        void RescheduleMessage(long id, DateTime deliveryDate)
        {
        }

        void MoveMessageToInputQueue(long id)
        {
        }

        void DeleteMessage(long id)
        {
        }

        long FindMessageByUniqueId(string uniqId)
        {
            throw new NotImplementedException();
        }

        

    }
}
