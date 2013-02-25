using System;
using NGinnBPM.MessageBus.Windsor;

namespace SubscriberOne
{
    class SubscriberOneHost
    {
        static void Main(string[] args)
        {
            NLog.Config.SimpleConfigurator.ConfigureForConsoleLogging(NLog.LogLevel.Warn);
            var mc = ConfigureMessageBus("sql://MessageBus/MQ_Events_SubscriberOne");
            var bus = mc.GetMessageBus();
            bus.SubscribeAt("sql://MessageBus/MQ_Events",typeof(Object));
            Console.WriteLine("Listening, press enter to exit...");
            //this sends a message to the publisher (and it will re-send it to us using pub-sub)
            bus.Send("sql://MessageBus/MQ_Events", new Messages.GreetingMessage {Text = "Hi, this is subscriber one speaking" });
            Console.ReadLine();
            mc.StopMessageBus();
        }


        static MessageBusConfigurator ConfigureMessageBus(string endpoint)
        {
            var mc = NGinnBPM.MessageBus.Windsor.MessageBusConfigurator.Begin()
                .ConfigureFromAppConfig() //use connection strings from the app.config file
                .SetEndpoint(endpoint)
                .AddMessageHandlersFromAssembly(typeof(SubscriberOneHost).Assembly) //register message handlers
                .AutoCreateDatabase(true) //queue tables will be created if they don't exist. Warning: you have to have 'create table' db permissions to do that!
                .SetEnableSagas(false) //disable saga for now
                .UseSqlSubscriptions()
                .SetAlwaysPublishLocal(false)
                .FinishConfiguration()
                .StartMessageBus(); //run the queue
            return mc;
        }
    }
}
