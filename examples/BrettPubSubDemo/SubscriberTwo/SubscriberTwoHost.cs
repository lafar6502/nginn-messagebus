using System;
using NGinnBPM.MessageBus.Windsor;

namespace SubscriberTwo
{
    /// <summary>
    /// 
    /// </summary>
    class SubscriberTwoHost
    {
        static void Main(string[] args)
        {
            NLog.Config.SimpleConfigurator.ConfigureForConsoleLogging(NLog.LogLevel.Warn);
            MessageBusConfigurator mc = ConfigureMessageBus("sql://MessageBus/MQ_Events_SubscriberTwo");
            var bus = mc.GetMessageBus();
            //we'll be receiving events through the message distributor!
            //string eventHub = "sql://MessageBus/MQ_EventHub";
            string eventHub = "sql://MessageBus/MQ_Events";
            bus.SubscribeAt(eventHub, typeof(Object));
            Console.WriteLine("Subscribed at {0}", eventHub);
            Console.WriteLine("Subscriber two listening at {0}, press enter to exit...", bus.Endpoint);
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
