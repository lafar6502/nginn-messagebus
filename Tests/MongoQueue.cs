using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NGinnBPM.MessageBus;
using NGinnBPM.MessageBus.Impl;
using NGinnBPM.MessageBus.Windsor;
using NGinnBPM.MessageBus.Mongo;
using Castle.Windsor;
using Castle.MicroKernel;
using Castle.MicroKernel.Registration;
using NGinnBPM.MessageBus.Mongo.Windsor;
using NLog;
using MongoDB.Driver;
using MongoDB.Bson.IO;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.IO;

namespace Tests
{
    public class MongoQueue
    {
        

        public static IMessageBus ConfigureMongoBus()
        {
            IMessageBus bus = MessageBusConfigurator.Begin()
                .SetEndpoint("mongodb://localhost:27017/cogmon?queueName=Queue1")
                .SetEnableSagas(true)
                .SetMaxConcurrentMessages(8)
                .SetReuseReceiveConnectionForSending(true)
                .UseMongoDb()
                .AddMessageHandlersFromAssembly(typeof(MongoQueue).Assembly)
                .AutoStartMessageBus(true)
                .FinishConfiguration()
                .GetMessageBus();
            return bus;
        }

        public static void Test1()
        {
            var mb = ConfigureMongoBus();
            for (int i = 0; i < 10000; i++)
            {
                mb.Notify(new TestMessage1 { Id = 101 + i });
            }    
        }

        public static void Test2()
        {
            var mb = ConfigureMongoBus();
            using (var ts = new System.Transactions.TransactionScope())
            {
                for (int i = 0; i < 10000; i++)
                {
                    mb.Notify(new TestMessage1 { Id = 101 + i });
                }
                ts.Complete();
            }
        }

        public static void TestSagas()
        {
           
            var mb = ConfigureMongoBus();
            
            for (int i = 0; i < 100; i++)
            {
                mb.Notify(new SagaMessage1 { Id = i.ToString(), Num = i + 3 });
                mb.Notify(new SagaMessage2 { Num = i, Text = i.ToString() });
            }

            Console.ReadLine();
        }

        public static void TestSer()
        {
            BsonClassMap.RegisterClassMap<SagaTest.MyState>(
                x =>
                {
                    x.AutoMap();
                    x.SetIgnoreExtraElements(true);
                    
                });
            
            
            var bd = new MongoDB.Bson.BsonDocument();
            MongoDB.Bson.IO.BsonDocumentWriter bdw = (BsonDocumentWriter) BsonDocumentWriter.Create(bd);
            
            var obj = new SagaTest.MyState { Collected = new List<int>(), Last = DateTime.Now };


            MongoDB.Bson.Serialization.BsonSerializer.Serialize(bdw, obj);
            bdw.Flush();
            bd["_t"] = MongoDB.Bson.Serialization.TypeNameDiscriminator.GetDiscriminator(obj.GetType());
            bd["version"] = "3823424";
            Console.WriteLine(bd.ToJson());
            Type t = TypeNameDiscriminator.GetActualType(bd["_t"].AsString);
            var obj2 = BsonSerializer.Deserialize(bd, t);
            Console.WriteLine("deser: " + obj2);
        }
    }
}
