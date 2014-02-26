using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NGinnBPM.MessageBus.Impl;
using MongoDB.Driver;
using MongoDB.Bson;
using MongoDB.Driver.Builders;

namespace NGinnBPM.MessageBus.Mongo
{
    internal class Subscription
    {
        public string _id { get; set; }
        public List<string> Subscribers { get; set; }

        public Subscription()
        {
            Subscribers = new List<string>();
        }
    }

    public class MongoDbSubscriptionService : ISubscriptionService
    {
        public MongoDatabase Db { get; set; }   
        public string CollectionName { get; set; }

        public IEnumerable<string> GetTargetEndpoints(string messageType)
        {
            var t = Db.GetCollection<Subscription>(CollectionName).FindOneById(messageType);
            if (t == null) return new string[] { };
            return t.Subscribers;
        }

        public void Subscribe(string subscriberEndpoint, string messageType, DateTime? expiration)
        {
            var r = Db.GetCollection<Subscription>(CollectionName).Update(Query.EQ("_id", messageType), Update.AddToSet("Subscribers", subscriberEndpoint), UpdateFlags.Upsert);
            if (r.DocumentsAffected == 0) throw new Exception("Failed to save");
        }

        public void Unsubscribe(string subscriberEndpoint, string messageType)
        {
            var r = Db.GetCollection<Subscription>(CollectionName).Update(Query.EQ("_id", messageType), Update.Pull("Subscribers", subscriberEndpoint), UpdateFlags.Upsert);
            if (r.DocumentsAffected == 0) throw new Exception("Failed to save");
        }

        public void HandleSubscriptionExpirationIfNecessary(string subscriberEndpoint, string messageType)
        {
            
        }
    }
}
