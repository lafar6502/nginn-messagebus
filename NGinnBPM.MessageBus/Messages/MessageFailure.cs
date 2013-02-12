using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NGinnBPM.MessageBus.Messages
{
    /// <summary>
    /// Notification about message processing failure,
    /// sent when message bus gives up processing of a message
    /// </summary>
    [Serializable]
    public class MessageFailure
    {
        public object OriginalMessage { get; set; }
        public string ErrorInformation { get; set; }
        public string SourceEndpoint { get; set; }
    }
}
