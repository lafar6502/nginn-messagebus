using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NGinnBPM.MessageBus.Messages
{
    /// <summary>
    /// This message will be sent to you when your subscription is about to expire.
    /// When handling this message you should re-subscribe or unsubscribe.
    /// </summary>
    public class SubscriptionExpiring : ControlMessage
    {
        public string MessageType { get; set; }
    }

    
}
