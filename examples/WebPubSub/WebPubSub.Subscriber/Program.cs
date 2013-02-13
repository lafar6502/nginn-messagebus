using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NGinnBPM.MessageBus;
using NGinnBPM.MessageBus.Windsor;
using NLog;

namespace WebPubSub.Subscriber
{
    class Program
    {
        static void Main(string[] args)
        {
            NLog.Config.SimpleConfigurator.ConfigureForConsoleLogging(LogLevel.Warn);
            var mc = MessageBusConfigurator.Begin()
                .ConfigureFromAppConfig()
                .AddMessageHandlersFromAssembly(typeof(Program).Assembly)
                .FinishConfiguration()
                .StartMessageBus();
            IMessageBus mb = mc.GetMessageBus();
            mb.SubscribeAt(System.Configuration.ConfigurationManager.AppSettings["PublisherEndpoint"], typeof(Object));
            Console.WriteLine("Subscriber at {0} ready to receive messages. Press Enter to exit", mb.Endpoint);
            Console.ReadLine();
            Console.WriteLine("Stopping...");
            mc.StopMessageBus();

        }
    }
}
