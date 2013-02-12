using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NGinnBPM.MessageBus;
using NGinnBPM.MessageBus.Messages;
using NLog;

namespace NGinnBPM.MessageBus.Impl
{
    /// <summary>
    /// Ping service, for testing NGinn MessageBus communication
    /// </summary>
    public class PingService : IMessageConsumer<Ping>
    {
        private static Logger log = LogManager.GetCurrentClassLogger();
        public IMessageBus MessageBus { get; set; }
        #region IMessageConsumer<Ping> Members

        public void Handle(Ping message)
        {
            log.Info("Ping[{0}] from {1} sent at {2}. Delivery time: {3}", message.Id, MessageBus.CurrentMessageInfo.Sender, message.SentDate, DateTime.Now - message.SentDate);
            MessageBus.Reply(new Pong { Id = message.Id, SentDate = DateTime.Now, PingSentDate = message.SentDate });
        }

        #endregion
    }
}
