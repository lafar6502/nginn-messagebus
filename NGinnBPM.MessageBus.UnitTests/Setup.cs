using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Castle.Core;
using Castle.Windsor;
using NGinnBPM.MessageBus.Windsor;
using NGinnBPM.MessageBus;
using NGinnBPM.MessageBus.Impl;
using Castle.MicroKernel.Registration;
using System.Configuration;

namespace NGinnBPM.MessageBus.UnitTests
{
    public class Setup
    {
        public static IWindsorContainer ConfigureMessageBus(string endpointName, IDictionary<string, string> dbConnectionStrings)
        {
            MessageBusConfigurator cfg = MessageBusConfigurator.Begin()
                .SetEndpoint(endpointName)
                .SetConnectionStrings(dbConnectionStrings.Select((kv, i) => new ConnectionStringSettings { Name = kv.Key, ProviderName = "System.Data.SqlClient", ConnectionString = kv.Value }))
                .UseSqlSubscriptions()
                .UseStaticMessageRouting("Routing.json")
                //.RegisterHttpMessageServicesFromAssembly(typeof(Program).Assembly)
                .AddMessageHandlersFromAssembly(typeof(Setup).Assembly)
                .UseSqlSequenceManager()
                .SetEnableSagas(false)
                .SetSendOnly(false)
                .SetMaxConcurrentMessages(1)
                .SetUseTransactionScope(true)
                .SetAlwaysPublishLocal(false)
                .SetReuseReceiveConnectionForSending(true)
                .SetExposeReceiveConnectionToApplication(true)
                .SetDefaultSubscriptionLifetime(TimeSpan.FromHours(8))
                .AutoStartMessageBus(true);
            cfg.FinishConfiguration();
            return cfg.Container;
        }

        public static void Shutdown(IWindsorContainer wc)
        {
            MessageBusConfigurator.StopMessageBus(wc);
        }
    }
}
