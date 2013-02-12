using System;
using Messages;
using NGinnBPM.MessageBus;

namespace Receiver
{
    public class GreetingSubscriber : IMessageConsumer<GreetingMessage>
    {
        public void Handle(GreetingMessage message)
        {
            Console.WriteLine("Message Received: " + message.Text);
        }
    }
}
