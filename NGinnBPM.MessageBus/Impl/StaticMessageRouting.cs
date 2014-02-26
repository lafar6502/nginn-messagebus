using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using System.IO;
using NLog;

namespace NGinnBPM.MessageBus.Impl
{
    public class StaticMessageRouting : ISubscriptionService
    {
        public string ConfigFile { get; set; }
        private Dictionary<string, List<string>> _routes = null;
        private Logger log = LogManager.GetCurrentClassLogger();
        private static string[] empty = new string[0];

        public IEnumerable<string> GetTargetEndpoints(string messageType)
        {
            if (_routes == null) LoadConfig();
            var r = _routes;
            List<string> l;
            if (r.TryGetValue(messageType, out l)) return l;
            return empty;
        }

        private void LoadConfig()
        {
            if (File.Exists(ConfigFile))
            {
                using (StreamReader sr = new StreamReader(ConfigFile, Encoding.UTF8))
                {
                    JsonSerializer ser = new JsonSerializer();
                    _routes = ser.Deserialize<Dictionary<string, List<string>>>(new JsonTextReader(sr));
                }
            }
            else
            {
                log.Warn("Config file does not exist: {0}", ConfigFile);
                _routes = new Dictionary<string, List<string>>();
                _routes.Add("*", new List<string>(new string[] { "local" }));
            }
        }

        public void Subscribe(string subscriberEndpoint, string messageType, DateTime? expiration)
        {
            lock (this)
            {
                List<string> endpoints;
                if (!_routes.TryGetValue(messageType, out endpoints))
                {
                    endpoints = new List<string>();
                    _routes[messageType] = endpoints;
                }
                if (!endpoints.Contains(subscriberEndpoint)) endpoints.Add(subscriberEndpoint);
            }
        }

        public void Unsubscribe(string subscriberEndpoint, string messageType)
        {
            lock (this)
            {
                List<string> endpoints;
                if (!_routes.TryGetValue(messageType, out endpoints)) return;
                endpoints.Remove(subscriberEndpoint);
                if (endpoints.Count == 0) _routes.Remove(messageType);
            }
        }

        public void HandleSubscriptionExpirationIfNecessary(string subscriberEndpoint, string messageType)
        {
        }
    }
}
