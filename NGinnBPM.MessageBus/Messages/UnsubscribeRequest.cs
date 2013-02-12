using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NGinnBPM.MessageBus.Messages
{
    [Serializable]
    public class UnsubscribeRequest : IMessage
    {
        public string SubscriberEndpoint { get; set; }
        public string MessageType { get; set; }
    }
}
