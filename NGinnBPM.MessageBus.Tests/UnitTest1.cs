using System.Linq;
using NGinnBPM.MessageBus.Windsor;
using NLog;
using Castle.Windsor;
using System.Configuration;
using Castle.MicroKernel.Registration;
using NGinnBPM.MessageBus.Impl.HttpService;
using System.Data.Common;
using System.Transactions;
using NGinnBPM.MessageBus.Impl;


namespace NGinnBPM.MessageBus.Tests
{
    public class Tests
    {
        private IWindsorContainer _wc;
        private IMessageBus _bus;
        [SetUp]
        public void Setup()
        {
            DbProviderFactories.RegisterFactory("System.Data.SqlClient", System.Data.SqlClient.SqlClientFactory.Instance);
            var cs = new Dictionary<string, string>
            {
                {"testdb", "Data Source=tcp:cogitpc;Initial Catalog=AliplastConfigurator_copy;User Id=AliplastConfigurator;Password=PASS" }
            };
            _wc = ConfigureMessageBus("sql://testdb/MQ_Test", cs, null);

            _bus = _wc.Resolve<IMessageBus>();
        }

        public static IWindsorContainer ConfigureMessageBus(string endpointName, IDictionary<string, string> dbConnectionStrings, string httpUrl)
        {
            MessageBusConfigurator cfg = MessageBusConfigurator.Begin()
                .SetEndpoint(endpointName)
                .SetConnectionStrings(dbConnectionStrings.Select((kv, i) => new ConnectionStringInfo { Alias = kv.Key, ProviderName = "System.Data.SqlClient", ConnectionString = kv.Value }))
                .UseSqlSubscriptions()
                .UseStaticMessageRouting("Routing.json")
                //.RegisterHttpMessageServicesFromAssembly(typeof(Program).Assembly)
                .AddMessageHandlersFromAssembly(typeof(Tests).Assembly)
                .UseSqlSequenceManager()
                .SetEnableSagas(true)
                .SetSendOnly(false)
                .SetMaxConcurrentMessages(4)
                .SetUseTransactionScope(true)
                .SetAlwaysPublishLocal(true)
                .SetReuseReceiveConnectionForSending(true)
                .SetExposeReceiveConnectionToApplication(true)
                .SetDefaultSubscriptionLifetime(TimeSpan.FromHours(8))
                .CreateQueueTable("sql://testdb/MQ_Test2")
                .CreateQueueTable("sql://testdb/MQ_Polled")
                .AutoStartMessageBus(true);
            if (httpUrl != null)
                cfg.ConfigureHttpReceiver(httpUrl);
            cfg.CustomizeContainer(delegate (IWindsorContainer wc)
            {
                /*wc.Register(Component.For<NGinnBPM.MessageBus.Impl.ISerializeMessages>()
                    .ImplementedBy<NGinnBPM.MessageBus.Impl.ServiceStackMessageSerializer>()
                    .DependsOn(new { UseFullAssemblyNames = false })
                    .LifeStyle.Singleton);*/
                wc.Register(Component.For<IServlet>()
                    .ImplementedBy<FSDirectoryServlet>()
                    .DependsOn(new
                    {
                        MatchUrl = @"/www/(?<id>.+)?",
                        BaseDirectory = "c:\\inetpub\\wwwroot"
                    }).LifeStyle.Transient);
            });
            //cfg.ConfigureAdditionalSqlMessageBus("bus2", "sql://testdb1/MQueue2");
            cfg.FinishConfiguration();
            cfg.StartMessageBus();

            return cfg.Container;
        }

        public class TestMsg
        {
            public string Something { get; set; }
        }

        [Test]
        public void Test1()
        {
            using (var ts = new TransactionScope())
            {
                _bus.Notify(new TestMsg { Something = "la la la" });
                ts.Complete();
            }
            System.Threading.Thread.Sleep(5000);
            Console.WriteLine("Exitin..");
        }

        [Test]
        public void TestSendOut()
        {
            using (var ts = new TransactionScope())
            {
                _bus.Send("sql://testdb/MQ_Test2", new TestMsg { Something = "go away" });
                
                ts.Complete();
            }
            System.Threading.Thread.Sleep(5000);
            Console.WriteLine("Exitin..");
        }

        [Test]
        public void TestSendToPolledQueue()
        {
            var endp = "sql://testdb/MQ_Polled/Client1234";
            string remConn, remTable, clid;
            if (!SqlUtil.ParseSqlEndpoint(endp, out remConn, out remTable, out clid))
            {
                throw new Exception("Failed to parse sql endpoint...");
            }
            using (var ts = new TransactionScope())
            {
                _bus.Send(endp, new TestMsg { Something = "some client job..." });
                ts.Complete();
            }

        }
    }

    public class SomeTestHandler : IOutgoingMessageHandler<Tests.TestMsg>, IMessageConsumer<Tests.TestMsg>
    {
        public void Handle(Tests.TestMsg message)
        {
            Console.WriteLine("Handled test {0}", message.Something);
        }

        public void OnMessageSend(Tests.TestMsg message)
        {
            Console.WriteLine("Sending test {0}", message.Something);
        }
    }
}