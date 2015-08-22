using System;
using NGinnBPM.MessageBus;

namespace ExternalHandlerContainerExample
{
    public class PongMessageHandler : IMessageConsumer<PongMessage>
    {
        public void Handle(PongMessage message)
        {
            Console.WriteLine("** PONG handler at {2} received PongMessage '{0}' from {1}", message.ReplyText, MessageBusContext.CurrentMessage.Sender, MessageBusContext.CurrentMessageBus.Endpoint);
        }
    }
}
