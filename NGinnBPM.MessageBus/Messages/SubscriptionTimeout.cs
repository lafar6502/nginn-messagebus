using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NGinnBPM.MessageBus.Messages
{
    /// <summary>
    /// this message is used by the publisher to expire a subscription
    /// after its expiration time. It's sent to publisher local queue.
    /// </summary>
    public class SubscriptionTimeout
    {
        public string Subscriber { get; set; }
        public string MessageType { get; set; }
        public DateTime Expiration { get; set; }
    }
}
