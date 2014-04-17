using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NGinnBPM.MessageBus;

namespace BasicPingPongExample
{
    public class SeqMessageHandler : IMessageConsumer<SeqMessage>
    {

        public void Handle(SeqMessage message)
        {
            Console.WriteLine("SEQ {0}", message.Order);
        }
    }
}
