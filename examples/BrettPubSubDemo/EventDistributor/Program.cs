using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NGinnBPM.MessageBus;
using NGinnBPM.MessageBus.Impl;
using NGinnBPM.MessageBus.Windsor;
using NLog;
using Castle.Windsor;
using Castle.MicroKernel.Registration;

namespace EventDistributor
{
    /// <summary>
    /// Event Distributor example
    /// This is an implementation of Event Distributor service.
    /// It receives incoming messages and then distributes them in Pub-Sub manner to all subscribers.
    /// Therefore it acts as an event hub - multiple applications can send events to the distributor queue
    /// and the distributor will take care of delivering these events to their subscribers. This way the subscribers
    /// don't have to know all possible event sources and can subscribe at the hub instead.
    /// </summary>
    class Program
    {
        static void Main(string[] args)
        {
            NLog.Config.SimpleConfigurator.ConfigureForConsoleLogging(LogLevel.Info);
            var mc = ConfigureMessageBus();
            var bus = mc.GetMessageBus();
            Console.WriteLine("Event distributor is running at {0}. Press Enter to exit.", bus.Endpoint);
            var text = Console.ReadLine();
            mc.StopMessageBus();
        }

        static MessageBusConfigurator ConfigureMessageBus()
        {
            var mc = NGinnBPM.MessageBus.Windsor.MessageBusConfigurator.Begin()
                .ConfigureFromAppConfig() //use connection strings from the app.config file
                .UseSqlSubscriptions()
                .AutoCreateDatabase(true) //queue tables will be created if they don't exist. Warning: you have to have 'create table' db permissions to do that!
                .SetEnableSagas(false) //disable saga for now
                .SetAlwaysPublishLocal(false)
                .CustomizeContainer(wc =>
                {
                    wc.Register(Component.For<IPreprocessMessages>().ImplementedBy<EventDistributionPreprocessor>().LifeStyle.Singleton);
                })
                .FinishConfiguration()
                .StartMessageBus(); //run the queue
            return mc;
        }
    }
}
