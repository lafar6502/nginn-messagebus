using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NLog;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.IO;
using System.Collections;

namespace Tests
{
    public class MessageWrapper
    {
        public MessageWrapper()
        {
            Headers = new Hashtable();
        }
        public Hashtable Headers { get; set; }
        public object Body { get; set; }
    }

    public class MessageWrapper2
    {
        public MessageWrapper2()
        {
            Headers = new Hashtable();
        }
        
        public Hashtable Headers { get; set; }
        public object Body { get; set; }
    }

    public enum SomeEnum
    {
        Value1,
        OtherValue,
        AndYetAnotherValue
    }

    public class SomeMessage
    {
        public SomeMessage() 
        {
        }

        public SomeMessage(bool b)
        {
            Id = "asdfasd9832923";
            V = 38923323;
            D = DateTime.Now;
            Arr2 = new List<string>();
            for (int i=0; i<10; i++)
                Arr2.Add("$A" + i);
            Arr = Arr2.ToArray();
            Dic = new Dictionary<string,object>();
            foreach(string s in Arr)
                Dic[s] = s + b;
            TheVal = b ? SomeEnum.OtherValue : SomeEnum.AndYetAnotherValue;
        }

        public string Id { get; set; }
        public int V { get; set; }
        public string Body { get; set; }
        public string[] Arr { get; set; }
        public List<string> Arr2 { get; set; }
        public DateTime D { get; set; }
        public Dictionary<string, object> Dic {get; set;}
        public SomeEnum TheVal { get; set; }
    }

    public class TemplatedMessage<T>
    {
        public TemplatedMessage()
        {
            Body = new List<T>();
        }

        public TemplatedMessage(T b)
        {
            Body = new List<T>();
            Body.Add(b);
        }

        public List<T> Body { get; set; }
    }

    public class HashtableCreationHandler : Newtonsoft.Json.Converters.CustomCreationConverter<Hashtable>
    {
        public override Hashtable Create(Type objectType)
        {
            return new Hashtable();
        }
    }

    public class BodyCreationHandler : Newtonsoft.Json.Converters.CustomCreationConverter<object>
    {
        public override object Create(Type objectType)
        {
            return Activator.CreateInstance(objectType);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            return base.ReadJson(reader, objectType, existingValue, serializer);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            JsonSerializerSettings jss = new JsonSerializerSettings();
            jss.TypeNameHandling = TypeNameHandling.Objects;
            writer.WriteRawValue(JsonConvert.SerializeObject(value, Formatting.None, jss));
        }
    }

    public class SerializationTests
    {
        private static Logger log = LogManager.GetCurrentClassLogger();

        public static void  Test1()
        {
            JsonSerializerSettings s = new JsonSerializerSettings();
            s.TypeNameHandling = TypeNameHandling.Objects;
            JsonSerializer ser = JsonSerializer.Create(s);
            MessageWrapper2 mw2 = new MessageWrapper2();
            mw2.Headers["DeliverAt"] = "yesterday!";
            mw2.Body = new TestMessage1 { Id = 893239 };
            StringWriter sw = new StringWriter();
            ser.Serialize(sw, mw2);
            log.Info("Serialized: {0}", sw.ToString());
            MessageWrapper2 mw3 = (MessageWrapper2) ser.Deserialize(new JsonTextReader(new StringReader(sw.ToString())));
            sw = new StringWriter();
            SerializeCustom(mw2, sw);
            log.Info("Serialized custom: {0}", sw.ToString());
            mw3 = (MessageWrapper2)ser.Deserialize(new JsonTextReader(new StringReader(sw.ToString())));

        }

        public static void SerializeCustom(MessageWrapper2 mw2, TextWriter tw)
        {
            JsonSerializerSettings s = new JsonSerializerSettings();
            s.TypeNameHandling = TypeNameHandling.Objects;
            using (JsonWriter jw = new JsonTextWriter(tw))
            {
            jw.WriteStartObject();
            jw.WritePropertyName("Headers");
            jw.WriteRawValue(JsonConvert.SerializeObject(mw2.Headers));
            jw.WritePropertyName("Body");
            jw.WriteRawValue(JsonConvert.SerializeObject(mw2.Body, Formatting.Indented, s));
            jw.WriteEndObject();
            jw.Flush();
            }
        }

        public static void Test3()
        {
            Dictionary<string, object> dic = new Dictionary<string, object>();
            dic["ala"] = "kot";
            dic["ola"] = null;
            JsonSerializerSettings s = new JsonSerializerSettings();
            s.TypeNameHandling = TypeNameHandling.Objects;
            JsonSerializer ser = JsonSerializer.Create(s);
            var sw = new StringWriter();
            ser.Serialize(sw, dic);
            Console.WriteLine("dic: {0}", sw.ToString());
            var ret = ser.Deserialize(new JsonTextReader(new StringReader(sw.ToString())));

        }

        public static object TestSerializationPerf(NGinnBPM.MessageBus.Impl.ISerializeMessages ser, object sm)
        {
            log.Info("Testing serialization of {1} with {0}", ser.GetType().Name, sm.GetType());

            MemoryStream ms = new MemoryStream();
            StreamWriter sw = new StreamWriter(ms);
            ser.Serialize(sm, sw);
            sw.Flush();
            log.Info("SomeMessage size: {0}", ms.Length);
            ms.Seek(0, SeekOrigin.Begin);
            log.Info("{0}", new StreamReader(ms).ReadToEnd());
            int cnt = 100000;
            sw = new StreamWriter(ms);
            DateTime dt = DateTime.Now;
            for (int i = 0; i < cnt; i++)
            {
                ms.Seek(0, SeekOrigin.Begin);
                ser.Serialize(sm, sw);
                sw.Flush();
            }
            log.Info("Serialization time: {0}", DateTime.Now - dt);
            ms.Seek(0, SeekOrigin.Begin);
            StreamReader sr = new StreamReader(ms);
            dt = DateTime.Now;
            object sm2 = null;
            for (int i = 0; i < cnt; i++)
            {
                ms.Seek(0, SeekOrigin.Begin);
                sm2 = ser.Deserialize(sr);
            }
            log.Info("De-Serialization time: {0}", DateTime.Now - dt);
            return sm2;
        }

    }
}
