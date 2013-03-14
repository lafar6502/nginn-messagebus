using System;
using Messages;
using NGinnBPM.MessageBus;

namespace SubscriberTwo
{
    public class GreetingSubscriber : IMessageConsumer<GreetingMessage>
    {
       

        public void Handle(GreetingMessage message)
        {
            Console.WriteLine("SUB2: Message Received: " + message.Text);
        }
    }
}
