using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NGinnBPM.MessageBus.Windsor;
using NGinnBPM.MessageBus;
using NGinnBPM.MessageBus.Impl;
using Castle.MicroKernel.Registration;
using Castle.Windsor;
using NGinnBPM.MessageBus.Impl.Sagas;
using MongoDB.Driver;
using MongoDB.Bson.Serialization;

namespace NGinnBPM.MessageBus.Mongo.Windsor
{
    public static class MessageBusConfigurator4Mongo
    {

        static void ConfigureDefaultMongoDbIfNecessary(MessageBusConfigurator cfg)
        {
            string connstr, collection;
            if (!Util.ParseMongoEndpoint(cfg.Endpoint, out connstr, out collection))
                throw new Exception("Invalid mongo connection string: " + cfg.Endpoint);
            var dic = new Dictionary<string, string>();
            foreach(var cs in cfg.GetConnectionStrings())
            {
                if (cs.Name == null || dic.ContainsKey(cs.Name)) continue;
                dic[cs.Name] = cs.ConnectionString;
            }
            var cstr = Util.GetMongoConnectionStringForEndpoint(cfg.Endpoint, dic);

            cfg.CustomizeContainer(wc =>
            {
                if (!MessageBusConfigurator.IsServiceRegistered(wc, typeof(MongoDatabase)))
                {
                    wc.Register(Component.For<MongoDatabase>()
                        .Instance(MongoDatabase.Create(cstr)));
                }
            });
        }

        public static MessageBusConfigurator UseMongoDbSubscriptions(this MessageBusConfigurator cfg)
        {
            ConfigureDefaultMongoDbIfNecessary(cfg);
            cfg.CustomizeContainer(wc =>
            {
                if (!MessageBusConfigurator.IsServiceRegistered(wc, typeof(ISubscriptionService)))
                {
                    wc.Register(Component.For<ISubscriptionService>().ImplementedBy<MongoDbSubscriptionService>()
                        .DependsOn(new
                        {
                            CollectionName = "NG_Subscriptions",
                            SubscriptionLifetime = cfg.SubscriptionLifetime
                        }).LifeStyle.Singleton);
                }
            });
            return cfg;
        }


        public static MessageBusConfigurator RegisterMongoSagaType<TSaga>(this MessageBusConfigurator cfg)
        {
            cfg.RegisterSagaType(typeof(TSaga));
            if (!BsonClassMap.IsClassMapRegistered(typeof(TSaga)))
            {
                BsonClassMap.RegisterClassMap<TSaga>(x =>
                {
                    x.AutoMap();
                    x.SetIgnoreExtraElements(true);
                });
            }
            
            return cfg;
        }
        
        public static MessageBusConfigurator UseMongoDbTransport(this MessageBusConfigurator cfg)
        {
            return UseMongoDbTransport(cfg, "mongodb");
        }
        
        public static MessageBusConfigurator UseMongoDbTransport(this MessageBusConfigurator cfg, string name)
        {
            if (string.IsNullOrEmpty(cfg.Endpoint)) throw new Exception("Endpoint not set");
            var cs = cfg.GetConnectionStrings();

            

            cfg.CustomizeContainer(wc =>
            {
                wc.Register(Component.For<IMessageTransport, IStartableService, IHealthCheck>()
                .ImplementedBy<MongoDBTransport>()
                .DependsOn(new
                {
                    ConnectionStrings = cs,
                    Endpoint = cfg.Endpoint,
                    MessageRetentionPeriod = cfg.MessageRetentionPeriod,
                    MessageLockTimeSec = 300,
                    MaxConcurrentMessages = cfg.MaxConcurrentReceivers
                }).LifeStyle.Singleton.Named("MessageTransport_" + name));
                
                wc.Register(Component.For<IMessageBus>()
                   .ImplementedBy<NGinnBPM.MessageBus.Impl.MessageBus>()
                   .DependsOn(new
                   {
                       BatchOutgoingMessagesInTransaction = cfg.BatchOutMessages,
                       UseTransactionScope = cfg.UseTransactionScope,
                       PublishLocalByDefault = cfg.AlwaysPublishLocal
                   })
                   .Parameters(Parameter.ForKey("transport").Eq("${MessageTransport_" + name + "}"))
                   .LifeStyle.Singleton);
            });

            
            return cfg;
        }
        
        public static MessageBusConfigurator UseMongoDbSagaRepository(this MessageBusConfigurator cfg)
        {
            return UseMongoDbSagaRepository(cfg, "NG_Saga");
        }

        public static MessageBusConfigurator UseMongoDbSagaRepository(this MessageBusConfigurator cfg, string sagaCollection)
        {
            ConfigureDefaultMongoDbIfNecessary(cfg);
            cfg.CustomizeContainer(wc =>
            {
                wc.Register(Component.For<ISagaRepository>().ImplementedBy<MongoDbSagaJsonRepository>()
                    .DependsOn(new
                    {
                        SagaCollection = sagaCollection
                    }).LifeStyle.Singleton);
            });
            return cfg;
        }

        public static MessageBusConfigurator UseMongoDb(this MessageBusConfigurator cfg)
        {
            cfg.UseMongoDbTransport();
            cfg.UseMongoDbSagaRepository();
            cfg.UseMongoDbSubscriptions();
            return cfg;
        }
    }
}
