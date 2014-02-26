using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NLog;
using NGinnBPM.MessageBus.Windsor;
using NGinnBPM.MessageBus;
using Castle.Windsor;
using System.Threading;
using System.Transactions;
using NGinnBPM.MessageBus.Impl;

namespace PerfTesting
{
    class Program
    {
        static void Main(string[] args)
        {
            NLog.Config.SimpleConfigurator.ConfigureForConsoleLogging(LogLevel.Debug);
            //var mb1 = NinjectTest.ConfigureMessageBus();
            //return;
            if (args.Length == 0)
            {
                //TestScheduled();
                TestLotsa();
                return;
            }
            var s = args[0];
            if (s == "1")
            {
                Configure("sql://nginn/MQ_PT2", false);
            }
            else if (s == "2")
            {
                //TestSending1();
                TestSendPrehandler();
                Console.ReadLine();
            }
            else if (s == "3")
            {
                TestSending2();
            }
            else if (s == "4")
            {
                TestReceiving1();
            }
            else if (s == "5")
            {
                TestMTSending2(10, 4);
            }
            else if (s == "6")
            {
                TestMTSending2(1, 4);
            }
            else if (s == "7")
            {
                TestMTSending2(1, 8);
            }
            else if (s == "8")
            {
                TestMTSending2(10, 8);
            }
            else if (s == "9")
            {
                TestDispatchTargets();
            }
            else throw new NotImplementedException();
        }

        public static IWindsorContainer Configure(string endpoint, bool sendOnly)
        {
            var mc = MessageBusConfigurator.Begin()
                .ConfigureFromAppConfig()
                .SetEndpoint(endpoint)
                .SetSendOnly(sendOnly)
                .AddMessageHandlersFromAssembly(typeof(Program).Assembly)
                .AutoStartMessageBus(true)
                .BatchOutgoingMessages(true
                )
                .FinishConfiguration();
            return mc.Container;
        }

        static void TestDispatchTargets()
        {
            var mc = Configure("sql://nginn/MQ_PT1", false);
            var mb = mc.Resolve<IMessageBus>() as MessageBus;
            var dests = mb.GetTargetQueuesForMessageType(typeof(TestMessage1));

        }

        static void TestSending1()
        {
            var mc = Configure("sql://nginn/MQ_PT1", false);
            IMessageBus mb = mc.Resolve<IMessageBus>();
            Thread.Sleep(1000);
            Console.WriteLine("Starting TestSending1");
            DateTime dt = DateTime.Now;
            int N = 100000;
            for (int i=0; i<N; i++)
            {
                mb.Send("sql://nginn/MQ_PT1", new TestMessage1 { Id = i.ToString() });
            }
            TimeSpan ts = DateTime.Now - dt;
            Console.WriteLine("Sent {0} messages in {1}, {2} msgs/second", N, ts, N / ts.TotalSeconds);
        }

        static void TestSendPrehandler()
        {
            var mc = Configure("sql://nginn/MQ_PT1", false);
            IMessageBus mb = mc.Resolve<IMessageBus>();
            Thread.Sleep(1000);
            Console.WriteLine("Starting TestSending1");
            DateTime dt = DateTime.Now;
            for (int i = 0; i < 3; i++)
            {
                mb.Send("sql://nginn/MQ_PT1", new TestMessage1 { Id = i.ToString() });
            }
            TimeSpan ts = DateTime.Now - dt;
            Console.WriteLine("Sent {0} messages in {1}, {2} msgs/second", N, ts, N / ts.TotalSeconds);
        }


        static void TestLotsa()
        {
            var mc = Configure("sql://nginn/MQ_PT1", false);
            var mb = mc.Resolve<IMessageBus>();
            var st = new System.Diagnostics.Stopwatch();
            st.Start();
            using (var ts = new TransactionScope(TransactionScopeOption.Required, new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted }))
            {
                for (int i = 0; i < 5000; i++)
                {
                    mb.Send("sql://nginn/MQ_PT2", new TestMessage1 { Id = i.ToString() });
                }
                ts.Complete();
            }
            st.Stop();
            Console.WriteLine("************************************************************* Inserted all in {0}", st.ElapsedMilliseconds);
            Console.ReadLine();
        }

        static void TestScheduled()
        {
            var mc = Configure("sql://nginn/MQ_PT1", false);
            var mb = mc.Resolve<IMessageBus>();
            for (int i = 0; i < 100; i++)
            {
                mb.SendAt(DateTime.Now.AddSeconds(5), mb.Endpoint, new TestMessageX { Id = i });
            }
            Console.ReadLine();
        }

        /// <summary>
        /// batched sending
        /// </summary>
        static void TestSending2()
        {
            var mc = Configure("sql://nginn/MQ_PT1", true);
            IMessageBus mb = mc.Resolve<IMessageBus>();
            Thread.Sleep(1000);
            Console.WriteLine("Starting TestSending2");
            DateTime dt = DateTime.Now;
            int N = 100000;
            int B = 10;
            var to = new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted, Timeout = TimeSpan.FromSeconds(30) };
            for (int i = 0; i < N / B; i++)
            {
                using (var sc = new TransactionScope(TransactionScopeOption.Required, to))
                {
                    for (int j = 0; j < B; j++)
                    {
                        mb.Send("sql://nginn/MQ_PT1", new TestMessage1 { Id = (i * B + j).ToString() });
                    }
                    sc.Complete();
                }
            }
            TimeSpan ts = DateTime.Now - dt;
            Console.WriteLine("Sent {0} messages in {1}, {2} msgs/second", N, ts, N / ts.TotalSeconds);
        }

        static void TestReceiving1()
        {
            var mc = Configure("sql://nginn/MQ_PT1", false);
            IMessageBus mb = mc.Resolve<IMessageBus>();
            Console.WriteLine("Now receiving. Enter to exit");
            Console.ReadLine();
        }

static long _cnt = 0;
static IMessageBus Bus { get; set; }
static int N;
static int B;

/// <summary>
/// batched sending
/// </summary>
static void TestMTSending2(int batchSize, int threads)
{
    var mc = Configure("sql://nginn/MQ_PT1", true);
    Bus = mc.Resolve<IMessageBus>();
    Thread.Sleep(1000);
    Console.WriteLine("Starting TestMTSending2");
    DateTime dt = DateTime.Now;
    N = 100000;
    B = batchSize;
    int T = threads;
    List<Thread> proc = new List<Thread>();
    for (int t = 0; t<T; t++)
    {
        var th = new Thread(new ThreadStart(ThreadedSend));
        proc.Add(th);
    }
    foreach (var th in proc)
    {
        th.Start();
    }
    foreach (var th in proc)
    {
        th.Join();
    }

    TimeSpan ts = DateTime.Now - dt;
    Console.WriteLine("TestMTSending2 sent {0} messages in {1}, {2} msgs/second", N, ts, N / ts.TotalSeconds);
}

static void ThreadedSend()
{
    var to = new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted, Timeout = TimeSpan.FromSeconds(30) };
    while (true)
    {
        using (var sc = new TransactionScope(TransactionScopeOption.Required, to))
        {
            for (int j = 0; j < B; j++)
            {
                var i = Interlocked.Increment(ref _cnt);
                if (i >= N) return;
                Bus.Send("sql://nginn/MQ_PT1", new TestMessage1 { Id = i.ToString() });
            }
            sc.Complete();
        }
    }
}
    }
}
