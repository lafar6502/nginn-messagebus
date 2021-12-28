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
            /// <summary>
            /// Handle message now (it has arrived in order and can be processed)
            /// </summary>
            HandleMessage,
            /// <summary>
            /// Delay message processing until the end of transaction (because another message from this sequence is processed now)
            /// </summary>
            RetryImmediately,
            /// <summary>
            /// Handle the message later (it has arrived out of order and we need to wait until it's time comes)
            /// </summary>
            Postpone
        }
        /// <summary>
        /// Further processing disposition
        /// </summary>
        public ProcessingDisposition MessageDispositon;
        /// <summary>
        /// Estimated wait time for a postponed message.
        /// </summary>
        public DateTime? EstimatedRetry { get; set; }
        /// <summary>
        /// Id of next message belonging to same sequence.
        /// This information is optional and will be returned only if the sequence manager knows 
        /// next message's ID
        /// </summary>
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
