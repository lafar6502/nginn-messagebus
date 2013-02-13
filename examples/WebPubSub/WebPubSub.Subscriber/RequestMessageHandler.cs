using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WebPubSub.Messages;
using NGinnBPM.MessageBus;

namespace WebPubSub.Subscriber
{
    class RequestMessageHandler : IMessageConsumer<RequestNotification>
    {
        public void Handle(RequestNotification message)
        {
            Console.WriteLine("Http request {0} from {1}", message.Url, message.ClientIP);
        }
    }
}
