using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NGinnBPM.MessageBus.Mongo
{
    /// <summary>
    /// MongoDB endpoint format
    /// 
    /// mongodb://connectionString?q=queue
    /// where
    /// connectionString - is 
    /// </summary>
    public class Util
    {
        public static readonly string MongoPrefix = "mongodb://";

        
        public static string FormatMongoEndpoint(string connstring, string queueName)
        {
            string s = connstring;
            if (!s.Contains('?')) return connstring + "?queueName=" + queueName;
            Dictionary<string, string> q;
            string baseUrl;
            if (!ParseQueryString(connstring, out q, out baseUrl)) return null;
            q.Remove("queueName");
            q["queueName"] = queueName;
            var ret = new StringBuilder();
            foreach (string k in q.Keys)
            {
                if (ret.Length == 0)
                    ret.Append("?");
                else
                    ret.Append("&");
                ret.AppendFormat("{0}={1}", k, q[k]);
            }
            return baseUrl + ret;
        }

        public static bool ParseMongoEndpoint(string endpoint, out string connStr, out string collectionName)
        {
            connStr = null;
            collectionName = null;
            Dictionary<string, string> q;
            string baseUrl;
            if (!ParseQueryString(endpoint, out q, out baseUrl)) return false;
            if (!q.ContainsKey("queueName")) return false;
            if (endpoint.StartsWith(MongoPrefix))
            {
                collectionName = q["queueName"];
                connStr = endpoint;
                return true;
            }
            else return false;
        }

        public static bool ParseQueryString(string url, out Dictionary<string, string> query, out string baseUrl)
        {
            var ur = new Uri(url);
            
            var dic = new Dictionary<string, string>();
            query = dic;
            var idx = url.IndexOf('?');
            if (idx < 0)
            {
                baseUrl = url;
                return true;
            }
            baseUrl = url.Substring(0, idx);
            var nvs = url.Substring(idx + 1).Split('&');
            foreach (string nv in nvs)
            {
                var kv = nv.Split('=');
                if (kv.Length == 1)
                    dic.Add(Uri.UnescapeDataString(kv[0]), "");
                else
                    dic.Add(Uri.UnescapeDataString(kv[0]), Uri.UnescapeDataString(kv[1]));
            }
            return true;
        }

        public static bool IsValidMongoEndpoint(string endpoint)
        {
            string x, y;
            return ParseMongoEndpoint(endpoint, out x, out y);
        }

        public static string GetMongoConnectionStringForEndpoint(string ep, IDictionary<string, string> connStrings)
        {
            string cs, cn;
            if (!Util.ParseMongoEndpoint(ep, out cs, out cn)) throw new Exception("Invalid endpoint: " + ep);
            string s1 = cs.Substring(Util.MongoPrefix.Length);
            int idx = s1.IndexOf('/');
            if (idx > 0) s1 = s1.Substring(0, idx);
            if (connStrings != null && connStrings.ContainsKey(s1))
            {
                return connStrings[s1];
            }
            var cstr = System.Configuration.ConfigurationManager.ConnectionStrings[s1];
            if (cstr != null)
            {
                return cstr.ConnectionString;
            }
            
            return cs;
        }
    }
}
