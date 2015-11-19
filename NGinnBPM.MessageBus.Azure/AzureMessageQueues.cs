using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NGinnBPM.MessageBus.Impl;
using Microsoft.ServiceBus.Messaging;
using System.IO;

namespace NGinnBPM.MessageBus.Azure
{
    public class AzureMessageQueues
    {
        public static void SendMessageBatch(IEnumerable<MessageContainer> messages, string destinationQueue)
        {
            
        }

        public static BrokeredMessage Package(MessageContainer mc)
        {
            var bm = new BrokeredMessage(new MemoryStream(Encoding.UTF8.GetBytes(mc.BodyStr)), true);
            bm.Label = mc.Label;
            bm.CorrelationId = mc.CorrelationId;
            bm.ContentType = "text/json";
            if (mc.DeliverAt > DateTime.Now)
            {
                bm.ScheduledEnqueueTimeUtc = mc.DeliverAt.ToUniversalTime();
            }
            if (mc.HasHeader(MessageContainer.HDR_TTL))
            {
                var ttl = mc.GetDateTimeHeader(MessageContainer.HDR_TTL, DateTime.MaxValue);
                if (ttl > DateTime.Now) bm.TimeToLive = ttl - DateTime.Now;
            }
            return bm;
        }


    }
}
