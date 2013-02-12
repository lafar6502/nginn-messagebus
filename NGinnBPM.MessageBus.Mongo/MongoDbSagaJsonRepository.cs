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
using Newtonsoft.Json;

namespace NGinnBPM.MessageBus.Mongo
{
    /// <summary>
    /// Saga state repository that serializes sagas using Newtonsoft json serializer
    /// Does not require you to configure bson mapping for your sagas
    /// </summary>
    public class MongoDbSagaJsonRepository : MongoDbSagaRepository
    {
        public class SagaHolder
        {
            public string _id { get; set; }
            public string version { get; set; }
            public string data { get; set; }
        }

        private JsonSerializer _ser;

        public MongoDbSagaJsonRepository()
        {
            _ser = JsonSerializer.Create(new JsonSerializerSettings { 
                MissingMemberHandling = MissingMemberHandling.Ignore,
                NullValueHandling = NullValueHandling.Ignore,
                TypeNameHandling = TypeNameHandling.Objects});

            if (!BsonClassMap.IsClassMapRegistered(typeof(SagaHolder)))
            {
                BsonClassMap.RegisterClassMap<SagaHolder>();
            }
        }

        public override bool Get(string id, Type stateType, bool forUpdate, out object state, out string version)
        {
            version = null;
            state = null;
            var sh = Db.GetCollection(SagaCollection).FindOneByIdAs<SagaHolder>(id);
            if (sh == null) return false;
            state = _ser.Deserialize(new JsonTextReader(new System.IO.StringReader(sh.data)));
            version = sh.version;
            return true;
        }

        

        public override void Update(string id, object state, string originalVersion)
        {
            var sw = new System.IO.StringWriter();
            _ser.Serialize(sw, state);
            if (string.IsNullOrEmpty(originalVersion)) throw new Exception("version");
            string newVersion = (Int32.Parse(originalVersion) + 1).ToString();
            var res = Db.GetCollection(SagaCollection).Update(B.Query.And(B.Query.EQ("_id", id), B.Query.EQ("version", originalVersion)), 
                B.Update.Set("data", sw.ToString()).Set("version", newVersion), SafeMode.True);
            if (res.DocumentsAffected == 0) throw new Exception("Concurrent modification of saga " + id);
        }

        public override void InsertNew(string id, object state)
        {
            var sh = new SagaHolder();
            var sw = new System.IO.StringWriter();
            _ser.Serialize(sw, state);
            sh.data = sw.ToString();
            sh.version = "1";
            sh._id = id;
            Db.GetCollection(SagaCollection).Insert(sh);
        }
    }
}
