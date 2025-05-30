using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using NGinnBPM.MessageBus;
using System.Diagnostics;
using Newtonsoft.Json.Serialization;

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

        public Type GetObjectType(string fullTypeName)
        {
            var ser = GetSerializer();
            string typeName, assemblyName;
            SplitFullyQualifiedTypeName(fullTypeName, out typeName, out assemblyName);
            return ser.SerializationBinder.BindToType(assemblyName, typeName);
        }

        public string GetTypeName(Type objectType)
        {
            var ser = GetSerializer();
            return GetFullyQualifiedTypeName(objectType, ser.SerializationBinder);
        }

        private static bool SplitFullyQualifiedTypeName(string fullyQualifiedTypeName, out string typeName, out string assemblyName)
        {
            int? assemblyDelimiterIndex = GetAssemblyDelimiterIndex(fullyQualifiedTypeName);

            if (assemblyDelimiterIndex != null)
            {
                typeName = Trim(fullyQualifiedTypeName, 0, assemblyDelimiterIndex.GetValueOrDefault());
                assemblyName = Trim(fullyQualifiedTypeName, assemblyDelimiterIndex.GetValueOrDefault() + 1, fullyQualifiedTypeName.Length - assemblyDelimiterIndex.GetValueOrDefault() - 1);
            }
            else
            {
                typeName = fullyQualifiedTypeName;
                assemblyName = null;
            }
            return true;
        }

        private static int? GetAssemblyDelimiterIndex(string fullyQualifiedTypeName)
        {
            // we need to get the first comma following all surrounded in brackets because of generic types
            // e.g. System.Collections.Generic.Dictionary`2[[System.String, mscorlib,Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089],[System.String, mscorlib, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089]], mscorlib, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089
            int scope = 0;
            for (int i = 0; i < fullyQualifiedTypeName.Length; i++)
            {
                char current = fullyQualifiedTypeName[i];
                switch (current)
                {
                    case '[':
                        scope++;
                        break;
                    case ']':
                        scope--;
                        break;
                    case ',':
                        if (scope == 0)
                        {
                            return i;
                        }
                        break;
                }
            }

            return null;
        }

        private static string Trim(string s, int start, int length)
        {
            // References: https://referencesource.microsoft.com/#mscorlib/system/string.cs,2691
            // https://referencesource.microsoft.com/#mscorlib/system/string.cs,1226
            if (s == null)
            {
                throw new ArgumentNullException();
            }
            if (start < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(start));
            }
            if (length < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(length));
            }
            int end = start + length - 1;
            if (end >= s.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(length));
            }
            for (; start < end; start++)
            {
                if (!char.IsWhiteSpace(s[start]))
                {
                    break;
                }
            }
            for (; end >= start; end--)
            {
                if (!char.IsWhiteSpace(s[end]))
                {
                    break;
                }
            }
            return s.Substring(start, end - start + 1);
        }

        private static string GetFullyQualifiedTypeName(Type t, ISerializationBinder? binder)
        {
            if (binder != null)
            {
                binder.BindToName(t, out string? assemblyName, out string? typeName);
#if (NET20 || NET35)
                // for older SerializationBinder implementations that didn't have BindToName
                if (assemblyName == null & typeName == null)
                {
                    return t.AssemblyQualifiedName;
                }
#endif
                return typeName + (assemblyName == null ? "" : ", " + assemblyName);
            }

            return t.AssemblyQualifiedName!;
        }
    }
}
