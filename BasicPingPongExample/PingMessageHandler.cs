using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NGinnBPM.MessageBus;

namespace BasicPingPongExample
{
    public class PingMessageHandler : IMessageConsumer<PingMessage>
    {
        public IMessageBus MessageBus { get; set; }

        public void Handle(PingMessage message)
        {
            Console.WriteLine("*  PING handler at {2} received Message '{0}' from {1}", message.Text, MessageBusContext.CurrentMessage.Sender, MessageBus.Endpoint);
            MessageBus.Reply(new PongMessage { ReplyText = "Hello, in reply to '" + message.Text + "'"});
        }
    }
}
