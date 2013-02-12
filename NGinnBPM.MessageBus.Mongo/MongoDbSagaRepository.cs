using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NGinnBPM.MessageBus.Impl;
using NGinnBPM.MessageBus.Sagas;
using NGinnBPM.MessageBus.Impl.Sagas;
using NGinnBPM.MessageBus;
using NLog;
using MongoDB.Driver;
using MongoDB.Bson;
using B = MongoDB.Driver.Builders;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.IO;

namespace NGinnBPM.MessageBus.Mongo
{
    /// <summary>
    /// Saga repository that stores the state in MongoDB directly as Bson documents.
    /// </summary>
    public class MongoDbSagaRepository : ISagaRepository
    {
        public MongoDatabase Db { get; set; }
        public string SagaCollection { get; set; }

        public MongoDbSagaRepository()
        {
            SagaCollection = "NGSagas";
        }

        public virtual bool Get(string id, Type stateType, bool forUpdate, out object state, out string version)
        {
            state = null;
            version = null;
            var st = Db.GetCollection(SagaCollection).FindOneById(id);
            if (st == null) return false;
            string tid;
            state = DeserializeState(st, out version, out tid);
            return true;
        }

        

        public virtual void Update(string id, object state, string originalVersion)
        {
            if (string.IsNullOrEmpty(originalVersion)) throw new Exception("version");
            string newVersion = (Int32.Parse(originalVersion) + 1).ToString();
            var bd = Serialize(state, newVersion, id);
            var res = Db.GetCollection(SagaCollection).Update(B.Query.And(B.Query.EQ("_id", id), B.Query.EQ("version", originalVersion)), 
                B.Update.Replace(bd), SafeMode.True);
            if (res.DocumentsAffected == 0) throw new Exception("Concurrent modification of saga " + id);
        }

        public virtual void Delete(string id)
        {
            Db.GetCollection(SagaCollection).Remove(B.Query.EQ("_id", id));
        }

        public virtual void InsertNew(string id, object state)
        {
            var bd = Serialize(state, "1", id);
            Db.GetCollection(SagaCollection).Insert(bd);
        }

        protected BsonDocument Serialize(object state, string version, string id)
        {
            BsonDocument bd = new BsonDocument();
            using (var bw = BsonWriter.Create(bd))
            {
                BsonSerializer.Serialize(bw, state);
            }
            string td =  TypeNameDiscriminator.GetDiscriminator(state.GetType());
            bd["_t"] = td;
            if (td.Length <= 10) throw new Exception("Short type name in " + id);
            bd["_id"] = id;
            if (!bd.Contains("version") && version != null) bd["version"] = version;
            return bd;
        }

        protected object DeserializeState(BsonDocument doc, out string version, out string id)
        {
            
            string t = doc["_t"].AsString;
            if (string.IsNullOrEmpty(t)) throw new Exception("No type discriminator in document");
            Type tt = TypeNameDiscriminator.GetActualType(t);
            if (tt == null) throw new Exception("Type not found: " + t);
            var ret = BsonSerializer.Deserialize(doc, tt);
            version = doc.Contains("version") ? doc["version"].AsString : null;
            id = doc.Contains("_id") ? doc["_id"].AsString : null;
            return ret;
        }
    }
}
