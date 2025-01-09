using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NGinnBPM.MessageBus.Impl
{
    public interface ISqlPolledQueue
    {
        string Endpoint
        {
            get;
        }

        /// <summary>
        /// get number of messages waiting in the queue for specified client ID
        /// if client ID null - get number of all messages wating
        /// </summary>
        /// <param name="clientId"></param>
        /// <returns></returns>
        int GetQueueLength(string clientId);

        /// <summary>
        /// report that message processing has failed
        /// </summary>
        /// <param name="messageId"></param>
        /// <param name="statusInfo"></param>
        void ReportMessageProcessingFailed(string messageId, string statusInfo);

        /// <summary>
        /// report that message handling is done 
        /// send back the result
        /// </summary>
        /// <param name="messageId"></param>
        /// <param name="result"></param>
        void ReportMessageProcessed(string messageId, object result);

        /// <summary>
        /// cancel processing of message sitting in the input queue
        /// </summary>
        /// <param name="messageId"></param>
        void CancelMessage(string messageId);
    }
}
