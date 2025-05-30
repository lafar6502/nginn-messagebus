using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NGinnBPM.MessageBus.Messages
{
    public class MessageHandlingFailure 
    {
        public string UniqueId { get; set; }
        public string BusMessageId { get; set; }
        public string MessageType { get; set; }
        public string CorrelationId { get; set; }
        /// <summary>
        /// queue where the message was located
        /// </summary>
        public string Queue { get; set; }
        /// <summary>
        /// error information
        /// </summary>
        public string StatusInfo { get; set; }
    }
    /// <summary>
    /// timeout for polled messages
    /// </summary>
    public class MessageHandlingTimeout : MessageHandlingFailure
    {
        
    }
}
