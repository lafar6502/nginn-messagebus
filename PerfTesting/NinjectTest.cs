using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Ninject;
using NGinnBPM.MessageBus;
using NGinnBPM.MessageBus.NinjectConfig;

namespace PerfTesting
{
    public class NinjectTest
    {
        
        public IServiceResolver ServiceResolver { get; set; }

        public static IMessageBus ConfigureMessageBus()
        {
            var mc = MessageBusConfigurator.Begin()
                .Test();
            mc.Container.Bind<NinjectTest>().ToSelf();
            var nt = mc.Container.Get<NinjectTest>();
            return null;
        }
    }
}
