using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using System.IO;
using NLog;

namespace NGinnBPM.MessageBus.Impl
{
    public class JsonMessageContainerSerializer : IMessageContainerSerializer
    {
        private JsonSerializer _ser;
        
        public MessageContainer Deserialize(string s, bool deserializeBody)
        {
            var mc = Deserialize(new StringReader(s));
            if (deserializeBody && !string.IsNullOrEmpty(mc.BodyStr))
            {
                var tn = mc.GetStringHeader(MessageContainer.HDR_MessageType, null);
                if (tn != null)
                {
                    mc.Body = _ser.Deserialize(new StringReader(mc.BodyStr), TypeFromString(tn));
                }
                else
                {
                    if (EmbedMessageTypeInBody)
                    {
                        mc.Body = _ser.Deserialize(new JsonTextReader(new StringReader(mc.BodyStr)));
                    }
                    else throw new Exception("Header missing: " + MessageContainer.HDR_MessageType);
                }
            }
            return mc;
        }

        public string Serialize(MessageContainer mc)
        {
            if (string.IsNullOrEmpty(mc.BodyStr))
            {
                if (mc.Body != null)
                {
                    
                    var sw = new StringWriter();
                    _ser.Serialize(sw, mc.Body);
                    if (!EmbedMessageTypeInBody)
                    {
                        mc.SetHeader(MessageContainer.HDR_MessageType, TypeToString(mc.Body.GetType()));
                    }
                    mc.BodyStr = sw.ToString();
                    mc.Body = null;
                }
            }
            var sw2 = new StringWriter();
            _ser.Serialize(sw2, mc);
            return sw2.ToString();
        }

        protected MessageContainer Deserialize(TextReader tr)
        {
            JsonReader jr = new JsonTextReader(tr);
            var mc = _ser.Deserialize<MessageContainer>(jr);
            return mc;
        }

        private Dictionary<string, Type> _types = new Dictionary<string, Type>();
        
        public bool UseFullAssemblyNames { get; set; }
        
        public bool EmbedMessageTypeInBody { get; set; }
        


        private string TypeToString(Type t)
        {
            if (UseFullAssemblyNames)
                return t.AssemblyQualifiedName;
            else
                return string.Format("{0}, {1}", t.FullName, t.Assembly.GetName().Name);
            //return t.AssemblyQualifiedName;
        }

        private Type TypeFromString(string tn)
        {
            Type t;
            if (!_types.TryGetValue(tn, out t))
            {
                try
                {
                    t = Type.GetType(tn);
                }
                catch (Exception ex)
                {
                    throw new Exception(string.Format("Type not found: '{0}'", tn));
                }
                if (t == null) throw new Exception(string.Format("Type not found: '{0}'", tn));
                _types[tn] = t;
            }
            return t;
        }
    }
}
