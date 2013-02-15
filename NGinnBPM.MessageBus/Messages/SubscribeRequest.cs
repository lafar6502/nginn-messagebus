using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NGinnBPM.MessageBus.Messages
{
    /// <summary>
    /// Message subscription request sent to publisher endpoint
    /// </summary>
    public class SubscribeRequest 
    {
        /// <summary>
        /// Endpoint of the subscriber. If not filled, sender's endpoint will be used.
        /// If you want to subscribe by http transport, put http endpoint here.
        /// </summary>
        public string SubscriberEndpoint { get; set; }
        /// <summary>
        /// Full message type name, including namespace but excluding assembly name.
        /// Hope this is enough to identify a message type.
        /// </summary>
        public string MessageType { get; set; }
        /// <summary>
        /// Desired subscription lifetime
        /// </summary>
        public TimeSpan SubscriptionTime { get; set; }
    }
}
