using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NGinnBPM.MessageBus.Messages;

namespace NGinnBPM.MessageBus.Impl
{
    /// <summary>
    /// Handles persistent subscription requests
    /// </summary>
    public class SubscriptionMsgHandler : 
        IMessageConsumer<SubscribeRequest>, 
        IMessageConsumer<UnsubscribeRequest>,
        IMessageConsumer<SubscriptionExpiring>,
        IMessageConsumer<SubscriptionTimeout>
    {
        public TimeSpan DefaultSubscriptionLifetime { get; set; }

        public SubscriptionMsgHandler(ISubscriptionService subscriptionService)
        {
            _sub = subscriptionService;
        }

        public IMessageBus MessageBus { get; set; }

        private ISubscriptionService _sub;

        #region IMessageConsumer<SubscribeRequest> Members

        public void Handle(NGinnBPM.MessageBus.Messages.SubscribeRequest message)
        {
            string subscriber = message.SubscriberEndpoint;
            if (subscriber == null || subscriber.Length == 0)
            {
                subscriber = MessageBus.CurrentMessageInfo.Sender;
            }
            DateTime? askRenew = null;
            DateTime? expiration = null;
            if (message.SubscriptionTime > TimeSpan.Zero)
            {
                askRenew = DateTime.Now + message.SubscriptionTime;
                expiration = DateTime.Now + message.SubscriptionTime + message.SubscriptionTime + TimeSpan.FromMinutes(1);
            }
            _sub.Subscribe(subscriber, message.MessageType, expiration);
            if (expiration.HasValue)
            {
                //set expiration to 2xlifetime so we have time to handle renewal
                MessageBus.SendAt(askRenew.Value, subscriber, new SubscriptionExpiring { MessageType = message.MessageType });
                MessageBus.SendAt(expiration.Value, MessageBus.Endpoint, new SubscriptionTimeout { Subscriber = subscriber, MessageType = message.MessageType, Expiration = expiration.Value });
            }
        }

        #endregion

        #region IMessageConsumer<UnsubscribeRequest> Members

        public void Handle(NGinnBPM.MessageBus.Messages.UnsubscribeRequest message)
        {
            _sub.Unsubscribe(message.SubscriberEndpoint, message.MessageType);
        }

        #endregion

        public void Handle(SubscriptionExpiring message)
        {
            //SubscriptionExpiring message came from our publisher.
            //Renew the subscription by replying with subscribe request
            MessageBus.Reply(new SubscribeRequest
            {
                MessageType = message.MessageType,
                SubscriberEndpoint = MessageBus.Endpoint,
                SubscriptionTime = DefaultSubscriptionLifetime
            });
        }



        public void Handle(SubscriptionTimeout message)
        {
            _sub.HandleSubscriptionExpirationIfNecessary(message.Subscriber, message.MessageType);
        }
    }
}
