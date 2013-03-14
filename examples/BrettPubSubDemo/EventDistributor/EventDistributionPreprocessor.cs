using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NGinnBPM.MessageBus;
using NGinnBPM.MessageBus.Impl;
using Newtonsoft.Json;
using NLog;
using System.Text.RegularExpressions;
using System.IO;

namespace EventDistributor
{
    /// <summary>
    /// This class is called before an incoming message is deserialized by nginn-messagebus
    /// so we are able to intercept all messages and forward them to subscribers
    /// The problem is that we don't deserialize messages and still need to know their type
    /// So we extract the message type from serialized Json string
    /// Unfortunately, even if we know the type name, we still don't know its inheritance hierarchy
    /// so there's no way we can send the message to base class subscribers...
    /// </summary>
    public class EventDistributionPreprocessor : IPreprocessMessages
    {
        public ISubscriptionService SubscriptionManager { get; set; }
        
        private Regex typeRe = new Regex("\\{\\\"\\$type\\\":\\\"(.*?)\\\"");
        private Logger log = LogManager.GetCurrentClassLogger();

        public MessagePreprocessResult HandleIncomingMessage(MessageContainer message, IMessageTransport transport)
        {
            log.Info("Preprocessing message: {0}", message.BodyStr);
            var m = typeRe.Match(message.BodyStr);
            if (!m.Success)
            {
                log.Warn("Did not match message type. Ignoring the message!");
                return MessagePreprocessResult.CancelFurtherProcessing;
            }
            string mtype = m.Groups[1].Captures[0].Value;
            int idx = mtype.IndexOf(',');
            if (idx > 0) mtype = mtype.Substring(0, idx);
            log.Info("Message type is {0}", mtype);
            if (mtype.StartsWith("NGinnBPM.MessageBus.Messages"))
            {
                log.Info("It's a control message so we process it as usual");
                return MessagePreprocessResult.ContinueProcessing;
            }
            List<MessageContainer> messages = new List<MessageContainer>();
            //now get destination endpoints from the subscribers database
            //and prepare a message clone for each destination
            List<string> destinations = new List<string>();
            foreach (string typeName in new string[] { mtype, "System.Object" })
            {
                foreach (string destination in SubscriptionManager.GetTargetEndpoints(typeName))
                {
                    if (!destinations.Contains(destination)) destinations.Add(destination);
                }
            }
            foreach (string destination in destinations)
            {
                var msg = message.Clone() as MessageContainer;
                msg.To = destination; //set destination. We don't update msg.From so the original endpoint is unchanged
                messages.Add(msg);
            }

            if (messages.Count > 0)
            {
                //send messages to their destinations
                transport.SendBatch(messages, MessageBusContext.ReceivingConnection);
                log.Info("Sent message to {0} destinations", messages.Count);
            }
            return MessagePreprocessResult.CancelFurtherProcessing;
        }
    }
}
