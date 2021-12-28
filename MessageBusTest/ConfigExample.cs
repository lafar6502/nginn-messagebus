using System;
using System.Collections.Generic;
using System.Text;
using NLog;
using NGinnBPM.MessageBus;
using System.Collections;
using NGinnBPM.MessageBus.Windsor;
using Castle.Windsor;

namespace Tests
{
    public class ConfigExample
    {

        public static IWindsorContainer ConfigureTheBus()
        {
            return MessageBusConfigurator.Begin()
                .ConfigureFromAppConfig()
                .AutoStartMessageBus(true)
                .FinishConfiguration()
                .Container;
        }

        public static IWindsorContainer ConfigureTheBusSendOnly()
        {
            return MessageBusConfigurator.Begin()
                .ConfigureFromAppConfig()
                .AutoStartMessageBus(true)
                .UseStaticMessageRouting("routing.json")
                .SetSendOnly(true)
                .FinishConfiguration()
                .Container;
        }

        public static void TestSendOnly()
        {
            var container = ConfigureTheBusSendOnly();
            var bus = container.Resolve<IMessageBus>();

            bus.Notify(new MessageNotUsedAnywhere { Really = "oh dear" });
        }


        public static void Test()
        {
            var wc = ConfigureTheBus();
            var bus = wc.Resolve<IMessageBus>();

            bus.Notify(new TestMessage1 { Id = 1234 });
        }
    }

    public class MessageNotUsedAnywhere
    {
        public string Really { get; set; }
    }
}
