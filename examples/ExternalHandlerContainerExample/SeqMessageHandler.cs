using System;
using NGinnBPM.MessageBus;

namespace ExternalHandlerContainerExample
{
    public class SeqMessageHandler : IMessageConsumer<SeqMessage>
    {

        public void Handle(SeqMessage message)
        {
            Console.WriteLine("SEQ {0}", message.Order);
        }
    }
}
