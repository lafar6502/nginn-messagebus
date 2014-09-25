using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NLog;
using Castle.Windsor;
using NGinnBPM.MessageBus;
using Castle.MicroKernel;
using Castle.MicroKernel.Registration;
using System.Collections;
using NGinnBPM.MessageBus.Windsor;
using NGinnBPM.MessageBus.Messages;
using System.Collections;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using NGinnBPM.MessageBus.Impl.HttpService;
using NGinnBPM.MessageBus.Sagas;
using NGinnBPM.MessageBus.Impl.Sagas;
using NGinnBPM.MessageBus.Impl;
using System.Transactions;
using System.Threading;

namespace Tests
{
    public class Program
    {
        private static Logger log = LogManager.GetCurrentClassLogger();

        static void Main(string[] args)
        {
            //NLog.Config.SimpleConfigurator.ConfigureForConsoleLogging(LogLevel.Info);
            
            try
            {
            	//OracleTests.TestBasicOps();
            	//OracleTests.TestDbInit();
            	//OracleTests.TestSend();
            	OracleTests.TestNamedQ();
            	Console.ReadLine();
            	return;
                //MongoQueue.Test1();
                //MongoQueue.TestSer();
                //MongoQueue.Test2();
                //MongoQueue.TestSagas();
                //return;
                //DelegateTest();
                //PerfTest.SendTest();
                //Console.ReadLine();
                //return;
                //SerializationTests.Test4();
                //SerializationTests.Test3();
                //turn;
                //CacheTest();
                //TransactionTest.Test1("Data Source=(local);Initial Catalog=NGinn;User Id=nginn;Password=PASS");
                //TransactionTest.Test2("Data Source=(local);Initial Catalog=NGinn;User Id=nginn;Password=PASS");
                //TransactionTest.Test3("Data Source=(local);Initial Catalog=NGinn;User Id=nginn;Password=PASS");

                //return;
                ///configure perf counters - just to see the stats in log
                //NGinnBPM.MessageBus.Perf.DefaultCounters.ConfigureFromFile("PerfCounters.xml");
                ///Map database alias to database connection string, so then you can use 
                ///the alias when referring to a queue: e.g. sql://testdb1/Queue1
                Dictionary<string, string> connStrings = new Dictionary<string,string>();
                connStrings["testdb1"] = "Data Source=(local);Initial Catalog=NGinn;User Id=nginn;Password=PASS";
                connStrings["testdb2"] = "Data Source=(local);Initial Catalog=NGinn;User Id=nginn;Password=PASS";
                ///configure two containers with two message buses
                IWindsorContainer wc1 = ConfigureMessageBus("sql://testdb1/MQueue1", connStrings, null);
                
                //IWindsorContainer wc2 = ConfigureMessageBus("sql://testdb2/MQueue2", connStrings, null);

                
                IMessageBus mb1 = wc1.Resolve<IMessageBus>();
                using (var ts = new TransactionScope())
                {
                	mb1.Notify(new TestMessage1 { });
                	mb1.Notify(new TestMessage1 { Id = 32423 });
                	var m = mb1 as NGinnBPM.MessageBus.Impl.MessageBus;
                	Console.WriteLine("STATE");
                	Console.WriteLine(m.GetCurrentTransactionState());
                	ts.Complete();
                }
                Console.ReadLine();
                return;
                //IMessageBus mb2 = wc1.Resolve<IMessageBus>("bus2");
                //SagaTest(wc1);
                
                //IMessageBus mb2 = wc2.Resolve<IMessageBus>();
                //mb1.SubscribeAt("sql://testdb2/MQueue2", typeof(TestMessage1));


                //mb1.NewMessage(new TestMessage1 { Id = 2 }).Send(mb2.Endpoint);
                
                //using (var ts = new System.Transactions.TransactionScope())
                //{
                //    mb1.NewMessage(new TestMessage1 { Id = 111 })
                //        .SetCorrelationId("123332")
                //        .Publish();
                //    //mb1.Notify(new TestMessage1 { Id = 100 });
                //    //mb1.Send("sql://testdb2/MQueue2", new TestMessage1());
                //    ts.Complete();
                //}
                Console.ReadLine();
                return;
                ServiceClient sc = new ServiceClient();
                sc.BaseUrl = "http://localhost:9013/call/";
                sc.CallService<TestMessage1>(new TestMessage1 { Id = 99 });
                sc.CallService<TestMessage1>(new TestMessage1 { Id = 100 });

                /*mb1.NewMessage(new Ping())
                    .SetTTL(DateTime.Now)
                    .Publish();
                mb1.Send("sql://testdb1/MQueue2", new Ping { });
                mb1.Notify(new TestMessage1 { Id = 1 });
                */
                //mb1.NewMessage(new TestMessage1()).InTransaction(mb1.CurrentMessageTransaction).Publish();
                Console.ReadLine();
                /*for (var i = 0; i < 40000; i++)
                {
                    mb1.Notify(new Pong { Id = i.ToString() });
                }*/
                Console.WriteLine("Done all");
                Console.ReadLine();
                /*var sid = Guid.NewGuid().ToString("N");
                for (int i = 10; i >= 0; i--)
                {
                    mb1.NewMessage(new Ping { Id = i.ToString() }).InSequence(sid, i, 11).Publish();
                }
                Console.ReadLine();
                return;*/
                //mb1.SubscribeAt("sql://testdb2/MQueue2", typeof(TestMessage1));
                //mb1.Send("sql://testdb2/MQueue2", new Ping { Id = "TEST" });
                    
                /*
                IMessageBus mb2 = wc2.Resolve<IMessageBus>();
                for (int i = 0; i < 50; i++)
                {
                    //mb1.Send("sql://testdb2/MQueue2", new Ping { Id = "TEST" + i });
                }

                mb1.Send("sql://testdb2/MQueue2", new Ping { Id = "test http" });
                for (int i = 0; i < 10; i++)
                {
                    mb1.NewMessage(new Ping { Id = "t1" })
                        .SetNonPersistentLocal()
                        .Publish();
                }*/
                DateTime dt = DateTime.Now;
                for (int j = 0; j < 100; j++)
                {
                    using (System.Transactions.TransactionScope ts = new System.Transactions.TransactionScope())
                    {
                        for (int i = 0; i < 100; i++)
                        {
                            //mb1.Send("sql://testdb2/MQueue2", new Ping { Id = (10 * j + i).ToString() });
                            ///send the message using fluent interface
                            mb1.NewMessage(new TestMessage1(j * 10 + i))
                                .SetDeliveryDate(DateTime.Now.AddSeconds(1))
                                .SetLabel("Test XX" + i)
                                .Publish();
                            
                        }
                        if (j % 2 < 2)
                        {
                            ts.Complete();
                        }
                    }
                }
                log.Warn("Inserted 10K messages in {0}", DateTime.Now - dt);
                Console.ReadLine();
            }
            catch (Exception ex)
            {
                log.Error("Error: {0}", ex);
                Console.ReadLine();
            }
        }
        
        public static IWindsorContainer ConfigureMessageBus(string endpointName, IDictionary<string, string> dbConnectionStrings, string httpUrl)
        {
            MessageBusConfigurator cfg = MessageBusConfigurator.Begin()
                .SetConnectionStrings(dbConnectionStrings)
                .SetEndpoint(endpointName)
                //.UseSqlSubscriptions()
                .UseStaticMessageRouting("Routing.json")
                //.RegisterHttpMessageServicesFromAssembly(typeof(Program).Assembly)
                .AddMessageHandlersFromAssembly(typeof(Program).Assembly)
                //.UseSqlSequenceManager()
                .SetEnableSagas(true)
                .SetSendOnly(false)
                .SetMaxConcurrentMessages(1)
                .SetUseTransactionScope(true)
                .SetAlwaysPublishLocal(false)
                .SetReuseReceiveConnectionForSending(true)
                .SetExposeReceiveConnectionToApplication(true)
                .SetDefaultSubscriptionLifetime(TimeSpan.FromHours(8))
                .AutoStartMessageBus(true);
            if (httpUrl != null)
                cfg.ConfigureHttpReceiver(httpUrl);
            cfg.CustomizeContainer(delegate(IWindsorContainer wc)
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
            cfg.ConfigureAdditionalSqlMessageBus("bus2", "sql://testdb1/MQueue2");
            cfg.FinishConfiguration();
            //cfg.StartMessageBus();

            return cfg.Container;
        }

        static void TestSerialization()
        {
            NGinnBPM.MessageBus.Impl.JsonMessageSerializer ser = new NGinnBPM.MessageBus.Impl.JsonMessageSerializer();
            MessageWrapper mw = new MessageWrapper();
            mw.Headers["Correlation-Id"] = "9389328492834.1";
            mw.Headers["Deliver-At"] = DateTime.Now.AddDays(1).ToString();
            mw.Body = new Ping { Id = "ala ma kota" };
            StringWriter sw = new StringWriter();
            ser.Serialize(mw, sw);
            log.Info("Serialized: {0}", sw.ToString());
            MessageWrapper mw2 = (MessageWrapper) ser.Deserialize(new StringReader(sw.ToString()));

            log.Info("MW2: {0}", mw2);
        }

        static void TestSerialization2()
        {
            JsonSerializerSettings settings = new JsonSerializerSettings();
            
        }

        static void CacheTest()
        {
            var cache = new NGinnBPM.MessageBus.Impl.SimpleCache<string, string>() {  };
            cache.MaxCapacity = 100;
            for (int i = 0; i < 150; i++)
            {
                var g = cache.Get(i.ToString(), x => "abc_" + i);
            }
            for (int i = 150; i > 0; i--)
            {
                var g = cache.Get(i.ToString(), x => "def_" + i);
            }
            
            Console.WriteLine("Hits: {0}", cache.HitRatio);

            for (int i = 150; i > 0; i--)
            {
                var g = cache.Get((i % 10).ToString(), x => "def_" + i % 10);
            }

            Console.WriteLine("Hits: {0}", cache.HitRatio);

            cache = new SimpleCache<string, string> { MaxCapacity = 10 };
            for (int i = 0; i < 100; i++)
            {
                ThreadPool.QueueUserWorkItem(new WaitCallback(delegate(object v)
                {
                    var r = cache.Get(v.ToString(), delegate(string t)
                    {
                        log.Info("Getting v for {0}", t);
                        Thread.Sleep(1000);
                        log.Info("Got v for {0}", t);
                        return "abc_" + t;
                    });
                }), i % 10);

            }
            Console.ReadLine();
            Console.WriteLine("Hits: {0}", cache.HitRatio);

        }

        static void SagaTest(IWindsorContainer wc)
        {
            IMessageBus mb = wc.Resolve<IMessageBus>();
            var id1= "SAGA_1";
            var id2 = "SAGA_2";
            using (TransactionScope ts = new TransactionScope())
            {
                mb.Notify(new SagaMessage1 { Id = id1, Num = 1});
                mb.Notify(new SagaMessage1 { Id = id2, Num = 1 });
                mb.Notify(new SagaMessage1 { Id = id1, Num = 2 });
                mb.Notify(new SagaMessage1 { Id = id2, Num = 2 });
                /*for (int i = 0; i < 10; i++)
                {
                    mb.NewMessage(new SagaMessage2 { Num = 100 + i, Text = "message2 " + i })
                        .SetCorrelationId(id1).Publish();

                    mb.NewMessage(new SagaMessage2 { Num = 100 + i, Text = "message2 " + i })
                        .SetCorrelationId(id2).Publish();

                }*/
                ts.Complete();
            }

        }

        static void DelegateTest()
        {
            Type t = typeof(IMessageConsumer<TestMessage1>);
            var d = DelegateFactory.CreateMessageHandlerDelegate(t.GetMethod("Handle"));

            var con = new TestHandler();
            d(con, new TestMessage1 { Id = 8392 });

            var c1 = DelegateFactory.CreateConstructorInvocation(typeof(TestHandler).GetConstructor(new Type[] {}));

        }
    }

    
}
