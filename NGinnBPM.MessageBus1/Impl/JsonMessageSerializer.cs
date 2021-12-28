using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using NGinnBPM.MessageBus;
using System.Diagnostics;

namespace NGinnBPM.MessageBus.Impl
{
    /// <summary>
    /// Serializes messages in Json format
    /// </summary>
    public class JsonMessageSerializer : ISerializeMessages
    {
        private Encoding _enc = Encoding.UTF8;
        public string TextEncoding
        {
            get { return _enc.EncodingName; }
            set { _enc = Encoding.GetEncoding(value); }
        }

        /// <summary>
        /// Format messages with indentation
        /// </summary>
        public bool PrettyPrint { get; set; }


        #region ISerializeMessages Members

        public void Serialize(object msg, System.IO.Stream stm)
        {
            StreamWriter sw = new StreamWriter(stm, _enc);
            Serialize(msg, sw);
            sw.Flush();
        }

        public object Deserialize(System.IO.Stream stm)
        {
            StreamReader sr = new StreamReader(stm, _enc);
            return Deserialize(sr);
        }

        #endregion

        private static JsonSerializerSettings _settings;
        protected virtual JsonSerializerSettings GetSerializerSettings()
        {
            JsonSerializerSettings s = _settings;
            if (s != null) return s;
            s = new JsonSerializerSettings();
            s.Converters.Add(new IsoDateTimeConverter());
            s.Converters.Add(new StringEnumConverter());
            if (AdditionalConverters != null)
            {
                foreach (JsonConverter c in AdditionalConverters)
                {
                    if (!s.Converters.Contains(c)) s.Converters.Add(c);
                }
            }
            s.TypeNameHandling = TypeNameHandling.Objects;
            s.NullValueHandling = NullValueHandling.Ignore;
            _settings = s;
            return s;
        }

        /// <summary>
        /// List of additional json converters that 
        /// should be used by the serializer
        /// </summary>
        public JsonConverter[] AdditionalConverters { get; set; }


        protected virtual JsonSerializer GetSerializer()
        {
            JsonSerializer ser = JsonSerializer.Create(GetSerializerSettings());
            return ser;
        }

        public void Serialize(object msg, TextWriter tw)
        {
            JsonSerializer ser = GetSerializer();
            JsonWriter jsw = new JsonTextWriter(tw);
            jsw.Formatting = PrettyPrint ? Formatting.Indented : Formatting.None;
            ser.Serialize(jsw, msg);
            jsw.Flush();
        }

        public object Deserialize(TextReader tr)
        {
            JsonSerializer ser = GetSerializer();
            JsonTextReader jtr = new JsonTextReader(tr);
            return ser.Deserialize(jtr);
        }
    }
}
