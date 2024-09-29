using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Linq;

namespace NGinnBPM.MessageBus.Tests
{
    public class ContainerTests
    {
        [SetUp]
        public void Setup()
        {
            
        }

        public interface ITestMe
        {
            string Something { get; }
        }

        public class T1 : ITestMe
        {
            public string Something { get; set; } = "T1";
        }

        public class T2 : ITestMe
        {
            public string Something { get; set; } = "T2";
        }

        public interface IMessageHTest<T> where T : class
        {
            void Test(T message);
        }

        public class AService : IMessageHTest<T1>, IMessageHTest<T2>
        {
            public void Test(T1 message)
            {
                Console.WriteLine("H {0}: {1}", this.GetHashCode(), message.Something);
            }

            public void Test(T2 message)
            {
                Console.WriteLine("H {0}: {1}", this.GetHashCode(), message.Something);
            }
        }

        [Test]
        public void Test1()
        {
            var services = new ServiceCollection();

            services.AddSingleton<ITestMe, T1>();
            services.AddSingleton<ITestMe, T2>();

            var container = services.BuildServiceProvider();

            var s = container.GetService<ITestMe>();

            Console.WriteLine("{0}", s.Something);

            var ss = container.GetServices<ITestMe>();
            foreach(var sss in ss)
            {
                Console.WriteLine("* {0}", sss.Something);
            }
        }

        [Test]
        public void TestMultiInterface()
        {
            var services = new ServiceCollection();

            services.AddSingleton<AService, AService>();
            services.AddTransient<IMessageHTest<T1>>(sc => {
                var rt = sc.GetService<AService>();
                return rt;
            });

            services.AddTransient<IMessageHTest<T2>>(sc =>
            {
                var rt = sc.GetService<AService>();
                return rt;
            });
            var container = services.BuildServiceProvider();

            var s1 = container.GetService<IMessageHTest<T1>>();
            var s2 = container.GetService<IMessageHTest<T2>>();
            var s0 = container.GetService<AService>();

            s1.Test(new T1());
            s2.Test(new T2());
            Assert.AreSame(s0, s2);
            Assert.AreSame(s0, s1);
        }

        [Test]
        public void TestNamed()
        {
            var services = new ServiceCollection();
            services.AddKeyedSingleton<ITestMe, T1>("K1");
            services.AddKeyedSingleton<ITestMe, T1>("K2");
            services.AddKeyedSingleton<ITestMe, T2>("K3");

            
            //services.AddKeyedSingleton<ITestMe, T2>(null);

            var container = services.BuildServiceProvider();

            
            var s0 = container.GetService<ITestMe>(); //NULL

            var s1 = container.GetKeyedService<ITestMe>("K1");
            var s2 = container.GetKeyedService<ITestMe>("K2");
            var s3 = container.GetKeyedService<ITestMe>("K3");

            var sss = container.GetServices<ITestMe>(); ///EMPTY collection returned

            Assert.AreEqual(3, sss.Count());
            
            
        }
    }
}