using System;
using NGinnBPM.MessageBus;
using SimpleInjector;

namespace ExternalHandlerContainerExample
{
    class Program
    {
        private static Container _container;

        /// <summary>
        /// Entry-level example of configuring the message bus and working with messages: sending, handling, pub-sub
        /// </summary>
        /// <param name="args"></param>
        static void Main(string[] args)
        {

            //Setup Simple Injector for handlers
            _container = new Container();
         
            _container.RegisterCollection(typeof(IMessageConsumer<>), new[] {typeof(Program).Assembly});

            _container.Verify();


            //NLog.Config.SimpleConfigurator.ConfigureForConsoleLogging(NLog.LogLevel.Warn);
            //uncomment only one example at a time 
            //TestSendReply();
            TestSeq();
            //TestPubSub(); //run pub-sub example
            // TestStaticRouting(); //static routing example
        }

        static void TestSeq()
        {
            string seqid = Guid.NewGuid().ToString();
            int len = 50;
            var mb = ConfigureMessageBus("sql://localdb/MQ_TestS1");
            for (int i = len - 1; i >= 0; i--)
            {
                mb.NewMessage(new SeqMessage { Order = i }).InSequence(seqid, i, len).Publish();
            }
            Console.WriteLine("Sent {0} messages in sequence {1}", len, seqid);
            Console.ReadLine();
            
        }


        static void TestSendReply()
        {
            //send-reply example
            //we are configuring two message buses in same process
            //this is to simplify the example, in normal case they should reside in separate processes
            //the 'localdb' connection string is configured in the app.config file (both queues are in same database)
            var senderBus = ConfigureMessageBus("sql://localdb/MQ_Test1");
            var recipientBus = ConfigureMessageBus("sql://localdb/MQ_Test2");
            Console.WriteLine("Configured two message buses - sender at '{0}' and recipient at '{1}'.\n Press Enter to continue...", senderBus.Endpoint, recipientBus.Endpoint);
            Console.ReadLine();
            for (int i = 0; i < 50; i++)
            {
                Console.WriteLine("Sending Ping...");
                senderBus.Send("sql://localdb/MQ_Test2", new PingMessage { Text = "Hi, this is me" });
            }
            
            //and waiting for a response
            Console.WriteLine("Now wait some time until messages are processed. Then hit Enter to exit");
            Console.ReadLine();

            //this example does not show how to shutdown the message bus
            //because we didn't keep a reference
            //to the configurator object that was used for creating the buses
            //but you should be able to figure it out
        }

        static void TestPubSub()
        {
            // pub-sub example
            // We are creating 3 queues, one for the publisher and two for subscribers
            // 1. both the publisher and subscribers are in the same process, but that's not a problem
            // 2. a Ping message will be delivered not only to both subscribers, but to the sender too 
            //    (this is because message is also published to the local queue by default)
            //    so as a result each published message will be received 3 times - once for each queue in this example
            //
            var distEndpoint = "sql://localdb/MQ_Test1";
            var senderBus = ConfigureMessageBus(distEndpoint);
            var recipient1 = ConfigureMessageBus("sql://localdb/MQ_Test2");
            var recipient2 = ConfigureMessageBus("sql://localdb/MQ_Test3");
            Console.WriteLine("Started 3 message buses - 1 sender and 2 recipients\n. Now will subscribe the recipients for Ping notifications");
            recipient1.SubscribeAt(distEndpoint, typeof(PingMessage));
            recipient2.SubscribeAt(distEndpoint, typeof(PingMessage));
            Console.WriteLine("Subscription requests sent (of course they need to be handled by the publisher before the subscription works). Enter to continue message publishing");
            Console.ReadLine();
            for (var i = 0; i < 10; i++)
            {
                senderBus.Notify(new PingMessage { Text = "Message " + i });
            }
            Console.WriteLine("Messages published. Wait for replies. When done, hit enter to exit");
            Console.ReadLine();
        }

        static void TestStaticRouting()
        {
            var publisher = ConfigureMessageBusWithStaticRouting("sql://localdb/MQ_Test1");
            var sub1 = ConfigureMessageBus("sql://localdb/MQ_Test2");
            var sub2 = ConfigureMessageBus("sql://localdb/MQ_Test3");

            Console.WriteLine("Configured publisher '{0}' and two subscribers '{1}' and '{2}', message distribution rules are in routing.json. Press Enter to continue", publisher.Endpoint, sub1.Endpoint, sub2.Endpoint);
            Console.ReadLine();
            
            for (var i = 0; i < 10; i++)
            {
                publisher.Notify(new PingMessage { Text = "Static routing " + i });
            }
            
            Console.WriteLine("Messages published. Wait until they are processed. Enter to exit");
            Console.ReadLine();
        }



        static IMessageBus ConfigureMessageBus(string endpoint)
        {
           
            var mc = NGinnBPM.MessageBus.Windsor.MessageBusConfigurator.Begin()
                .ConfigureFromAppConfig() //use connection strings from the app.config file
                .SetEndpoint(endpoint)
                .AutoCreateDatabase(true) //queue tables will be created if they don't exist. Warning: you have to have 'create table' db permissions to do that!
                .SetEnableSagas(false) //disable saga for now

                .UseSqlSequenceManager()
                .UseSqlSubscriptions() //use sql subscription storage
                .SetMaxConcurrentMessages(1)
                .UseExternalHandlerContainer(new MessageBusHandlerResolver(_container))
                .FinishConfiguration()
                .StartMessageBus(); //run the queue
            return mc.GetMessageBus();
        }

        static IMessageBus ConfigureMessageBusWithStaticRouting(string endpoint)
        {
            // configures a publisher message bus with routing rules in 'routing.json' file.
            // this file maps message types to their destinations (map: message type => list of subscriber endpoints)
            // "*" entry matches all message types
            var mc = NGinnBPM.MessageBus.Windsor.MessageBusConfigurator.Begin()
                .ConfigureFromAppConfig() //use connection strings from the app.config file
                .SetEndpoint(endpoint)
               // .AddMessageHandlersFromAssembly(typeof(Program).Assembly) //register message handlers
               .UseExternalHandlerContainer(new MessageBusHandlerResolver(_container))
                .AutoCreateDatabase(true) //queue tables will be created if they don't exist. Warning: you have to have 'create table' db permissions to do that!
                .SetEnableSagas(false) //disable saga for now
                .UseStaticMessageRouting("routing.json") //
                .SetAlwaysPublishLocal(false) //don't publish to local (publisher) queue by default
                .FinishConfiguration()
                .StartMessageBus(); //run the queue
            return mc.GetMessageBus();
        }

        


    }
}
