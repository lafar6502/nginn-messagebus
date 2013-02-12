using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NGinnBPM.MessageBus.Messages
{
    public class SubscriptionTimeout
    {
        public string Subscriber { get; set; }
        public string MessageType { get; set; }
        public DateTime Expiration { get; set; }
    }
}
