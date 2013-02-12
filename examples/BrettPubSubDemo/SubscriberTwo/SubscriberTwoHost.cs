using System;
using NGinnBPM.MessageBus.Windsor;

namespace SubscriberTwo
{
    class SubscriberTwoHost
    {
        static void Main(string[] args)
        {
            NLog.Config.SimpleConfigurator.ConfigureForConsoleLogging(NLog.LogLevel.Warn);
            MessageBusConfigurator mc = ConfigureMessageBus("sql://MessageBus/MQ_Events_SubscriberTwo");
            var bus = mc.GetMessageBus();
            bus.SubscribeAt("sql://MessageBus/MQ_Events",typeof(Object));
            Console.WriteLine("Listening, press enter to exit...");
            Console.ReadLine();
            mc.StopMessageBus();
        }


        static MessageBusConfigurator ConfigureMessageBus(string endpoint)
        {
            var mc = NGinnBPM.MessageBus.Windsor.MessageBusConfigurator.Begin()
                .ConfigureFromAppConfig() //use connection strings from the app.config file
                .SetEndpoint(endpoint)
                .AddMessageHandlersFromAssembly(typeof(SubscriberTwoHost).Assembly) //register message handlers
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
