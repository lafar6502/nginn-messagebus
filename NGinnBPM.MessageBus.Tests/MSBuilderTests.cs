using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NGinnBPM.MessageBus.Impl;
using NGinnBPM.MessageBus.MSDependencyInjection;
using NUnit.Framework;


namespace NGinnBPM.MessageBus.Tests
{
    public class MSBuilderTests
    {

        public static IServiceCollection ConfigureMessageBus(string endpointName, IDictionary<string, string> dbConnectionStrings, string httpUrl)
        {
            var sc = new ServiceCollection();
            var mb = MessageBusConfigBuilder.Begin(sc)
                .SetEndpoint(endpointName)
                .SetConnectionStrings(dbConnectionStrings)
                .UseSqlSubscriptions()
                .UseStaticMessageRouting("Routing.json")
                .AddMessageHandlersFromAssembly(typeof(Tests).Assembly)
                .UseSqlSequenceManager()
                .SetEnableSagas(false)
                .SetSendOnly(false)
                .FinishConfiguration();

            
            return sc;
        }

        [Test]
        public void TestSimplestBuild()
        {
            DbProviderFactories.RegisterFactory("System.Data.SqlClient", System.Data.SqlClient.SqlClientFactory.Instance);
            var cs = new Dictionary<string, string>
            {
                {"testdb", "Data Source=tcp:cogitpc;Initial Catalog=AliplastConfigurator_copy;User Id=AliplastConfigurator;Password=PASS" }
            };
            var sc = ConfigureMessageBus("sql://testdb/MQ_Test", cs, null);
            var sp = sc.BuildServiceProvider(true);
            var mb = sp.GetRequiredService<IMessageBus>();

            for(var i=0; i<20; i++)
            {
                mb.Notify(new Tests.TestMsg { Something = "bibibi " + i });
            }
            

            
        }

        [Test]
        public void TestBuildAndStart()
        {
            DbProviderFactories.RegisterFactory("System.Data.SqlClient", System.Data.SqlClient.SqlClientFactory.Instance);
            var cs = new Dictionary<string, string>
            {
                {"testdb", "Data Source=tcp:cogitpc;Initial Catalog=AliplastConfigurator_copy;User Id=AliplastConfigurator;Password=PASS" }
            };
            var sc = ConfigureMessageBus("sql://testdb/MQ_Test", cs, null);
            var sp = sc.BuildServiceProvider(true);
            
            foreach (var ss in sp.GetServices<IStartableService>())
            {
                Console.WriteLine("Starting {0}", ss.ToString());
                ss.Start();
            }

            var mb = sp.GetRequiredService<IMessageBus>();

            for (var i = 0; i < 20; i++)
            {
                mb.Notify(new Tests.TestMsg { Something = "bibibi " + i });
            }
            Thread.Sleep(10000);
            foreach (var ss in sp.GetServices<IStartableService>())
            {
                Console.WriteLine("Stopping {0}", ss.ToString());
                ss.Stop();
            }


        }

        public class M0
        {
            
        }

        public class M1 : M0
        {

        }

        public class Handler1 : IMessageConsumer<M0>
        {
            public void Handle(M0 message)
            {
                Console.WriteLine("Handler1.{0}", message.GetType().Name);
            }
        }

        public class Handler2 : IMessageConsumer<M1>
        {
            public void Handle(M1 message)
            {
                Console.WriteLine("Handler2.{0}", message.GetType().Name);
            }
        }


        [Test]
        public void TestDispatcher()
        {
            var sc = new ServiceCollection();
            sc.AddSingleton<IServiceResolver, NetDIServiceResolver>();
            sc.AddSingleton<IMessageDispatcher, MessageDispatcher>();

            DIHelper.RegisterHandlerType(typeof(Handler1), sc, false);
            DIHelper.RegisterHandlerType(typeof(Handler2), sc, false);

            var container = sc.BuildServiceProvider();


            var md = container.GetRequiredService<IMessageDispatcher>();

            md.DispatchMessage(new M0(), null);
            md.DispatchMessage(new M1(), null);


        }





    }
}
