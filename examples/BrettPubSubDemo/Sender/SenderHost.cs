using System;
using Messages;
using NGinnBPM.MessageBus.Windsor;

namespace Sender
{
    class SenderHost
    {
        static void Main(string[] args)
        {
            var mc = ConfigureMessageBus("sql://MessageBus/MQ_Test1");
            var bus = mc.GetMessageBus();
            Console.WriteLine("Message bus is up, enter a greeting");
            var text = Console.ReadLine();
            while (text != "q")
            {
                //Send to other endpoint
                bus.Send("sql://MessageBus/MQ_Test2", new GreetingMessage {Text = text});
                Console.WriteLine("Message Sent. Enter more text or 'q' to quit");
                text = Console.ReadLine();
            }
            mc.StopMessageBus();

        }

        static MessageBusConfigurator ConfigureMessageBus(string endpoint)
        {
            var mc = NGinnBPM.MessageBus.Windsor.MessageBusConfigurator.Begin()
                .ConfigureFromAppConfig() //use connection strings from the app.config file
                .SetEndpoint(endpoint)
                .AutoCreateDatabase(true) //queue tables will be created if they don't exist. Warning: you have to have 'create table' db permissions to do that!
                .SetEnableSagas(false) //disable saga for now
                .FinishConfiguration()
                .StartMessageBus(); //run the queue
            return mc;
            
        }
    }
}
