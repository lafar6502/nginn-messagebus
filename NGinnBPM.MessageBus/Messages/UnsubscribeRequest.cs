using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NGinnBPM.MessageBus.Messages
{
    public class UnsubscribeRequest : ControlMessage
    {
        public string SubscriberEndpoint { get; set; }
        public string MessageType { get; set; }
    }
}
