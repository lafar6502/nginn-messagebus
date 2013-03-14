using System;
using Messages;
using NGinnBPM.MessageBus.Windsor;
using Castle.MicroKernel.Registration;
using NGinnBPM.MessageBus.Impl;

namespace Publisher
{
    class PublishHost
    {
        static void Main(string[] args)
        {
            var mc = ConfigureMessageBus("sql://MessageBus/MQ_Events");
            var bus = mc.GetMessageBus();
            Console.WriteLine("Message bus is up, enter a greeting");
            var text = Console.ReadLine();
            while (text != "q")
            {
                bus.Notify(new GreetingMessage {Text = text});
                //to the event hub: bus.Send("sql://MessageBus/MQ_EventHub", new GreetingMessage { Text = "EventDist: " + text });
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
                .UseSqlSubscriptions()
                .AutoCreateDatabase(true) //queue tables will be created if they don't exist. Warning: you have to have 'create table' db permissions to do that!
                .SetEnableSagas(false) //disable saga for now
                .SetAlwaysPublishLocal(false)
                //disable the 'distributor' for now: .AddMessageHandlersFromAssembly(typeof(EventDistributor).Assembly)
                .FinishConfiguration()
                .StartMessageBus(); //run the queue
            return mc;
            
        }
    }
}
