using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NGinnBPM.MessageBus;
using System.Transactions;

namespace BasicPingPongExample
{
    public class PingMessageHandler : IMessageConsumer<PingMessage>
    {
        public IMessageBus MessageBus { get; set; }

        public void Handle(PingMessage message)
        {
            //Console.WriteLine("*  PING handler at {2} received Message '{0}' from {1}", message.Text, MessageBusContext.CurrentMessage.Sender, MessageBus.Endpoint);
            //MessageBus.Reply(new PongMessage { ReplyText = "Hello, in reply to '" + message.Text + "'"});
            Console.WriteLine("*  PING handler at {2} received Message '{0}' Generation {1} from {2}", message.Text,
                              message.Generation, MessageBusContext.CurrentMessage.Sender, MessageBus.Endpoint);

            using (var tran = new TransactionScope())
            {
                //RG if (message.Generation >= 3) return; //lets not have endless loop..

                //production version updates our database here .....

                //send another ping to self...
                MessageBus.SendAt(DateTime.Now.AddSeconds(10), "sql://localdb/MQ_Test2",
                                  new PingMessage { Text = message.Text, Generation = ++message.Generation });

                tran.Complete();
            }
        }
    }
}
