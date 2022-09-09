using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ninject;
using NGinnBPM.MessageBus;
using NGinnBPM.MessageBus.Impl;
using System.Configuration;

namespace NGinnBPM.MessageBus.NinjectConfig
{
    public class MessageBusConfigurator2
    {
        private List<ConnectionStringSettings> _connStrings = new List<ConnectionStringSettings>();
        private string _endpoint;
        public MessageBusConfigurator2 AddConnectionString(string alias, string connString, string providerName = null)
        {
            if (_connStrings.Any(x => x.Name == alias)) throw new Exception("Connection string already added: " + alias);
            _connStrings.Add(new ConnectionStringSettings
            {
                Name = alias,
                ConnectionString = connString,
                ProviderName = providerName ?? "System.Data.SqlClient"
            });
            return this;
        }

        public MessageBusConfigurator2 GetConnectionStringsFromAppConfig()
        {
            foreach (ConnectionStringSettings cs in ConfigurationManager.ConnectionStrings)
            {
                AddConnectionString(cs.Name, cs.ConnectionString);
            }
            return this;
        }

        public MessageBusConfigurator2 SetEndpoint(string s)
        {
            _endpoint = s;
            return this;
        }

        public MessageBusConfigurator2 ConfigureMessageBus(IKernel k)
        {

            k.Bind<IServiceResolver>().ToConstant(new NinjectServiceResolver(k));
            k.Bind<ISerializeMessages>().To<JsonMessageSerializer>().InSingletonScope();
            k.Bind<IMessageDispatcher>().To<MessageDispatcher>().InSingletonScope();
            

            k.Bind<IMessageTransport, IStartableService>().To<SqlMessageTransport2>().InSingletonScope().Named("messagetransport1")
                 .OnActivation((c, x) =>
                 {
                     //x.MessageRetentionPeriod = MessageRetentionPeriod;
                     //x.MaxConcurrentMessages = MaxConcurrentReceivers;
                     x.AutoCreateQueueTable = true;
                     //x.RequireHandler = true;
                     x.Endpoint = _endpoint;
                     x.ConnectionStrings = _connStrings;
                     x.SendOnly = false;

                     x.UseReceiveTransactionForSending = true;
                 });

            k.Bind<IMessageBus>().To<NGinnBPM.MessageBus.Impl.MessageBus>().InSingletonScope()
                .OnActivation((c, x) =>
                {
                    x.BatchOutgoingMessagesInTransaction = true;
                    x.PublishLocalByDefault = false;
                    x.UseTransactionScope = true;
                });

            return this;
        }



        public static void Test()
        {
            var k = new StandardKernel();
            var cf = new MessageBusConfigurator2();
            cf.GetConnectionStringsFromAppConfig()
                .SetEndpoint("sql://testdb/MQ_Test1")
                .ConfigureMessageBus(k);

            var mb = k.Get<IMessageBus>();

            var start = k.Get<IStartableService>();
            start.Start();


            Console.ReadLine();
        }
    }
}
