using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;

namespace NGinnBPM.MessageBus.Impl
{

    

    public class SequenceMessageDisposition
    {
        public enum ProcessingDisposition
        {
            HandleMessage,
            RetryImmediately,
            Postpone
        }

        public ProcessingDisposition MessageDispositon;
        public DateTime? EstimatedRetry { get; set; }
        public string NextMessageId { get; set; }
    }
    

    public interface ISequenceMessages
    {
        

        /// <summary>
        /// Register an arriving sequence message
        /// </summary>
        /// <param name="seqId">sequence id</param>
        /// <param name="seqNumber">message number in sequence, starts with 0</param>
        /// <param name="seqLen">sequence length, if known</param>
        /// <param name="transactionObj">Transaction, in case of SQL db it will be an IDbTransaction instance</param>
        /// <param name="lastSeen">Returns the date when previous sequence message has been seen</param>
        /// <returns>true if message should be handled, false when message should be postponed</returns>
        SequenceMessageDisposition SequenceMessageArrived(string seqId, int seqNumber, int? seqLen, object transactionObj, string messageId);
        
        //void SequenceMessageProcessed(string messageId, string sequenceId, int seqNumber, int? seqLen, object transactionObj);
    }
}
