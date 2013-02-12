using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using System.Net;
using NLog;
using System.IO;

namespace NGinnBPM.MessageBus
{
    public interface IServiceClient
    {
        T CallService<T>(string serviceName, object request);
        T CallService<T>(object request);
        object CallService(string serviceName, object request, Type responseType);
        void AddHeader(string name, string val);
    }
    /// <summary>
    /// This is a very simple Json-RPC client that can be used for
    /// calling 'web services' hosted by NGinn message bus HTTP service
    /// </summary>
    public class ServiceClient : IServiceClient
    {
        private JsonSerializer _ser;
        private Logger log = LogManager.GetCurrentClassLogger();
        private Dictionary<string, string> _hdr = null;

        public ServiceClient()
        {
            
        }

        /// <summary>
        /// Base url, e.g. http://localhost:8080/call/
        /// </summary>
        public string BaseUrl { get; set; }

        private JsonSerializer GetSerializer()
        {
            if (_ser != null) return _ser;
            var j = new JsonSerializer();
            j.DefaultValueHandling = DefaultValueHandling.Ignore;
            j.TypeNameHandling = TypeNameHandling.Objects;
            _ser = j;
            return j;
        }

        public T CallService<T>(object request)
        {
            return CallService<T>(request.GetType().Name, request);
        }
        /// <summary>
        /// Call a service
        /// </summary>
        /// <typeparam name="T">Response message type</typeparam>
        /// <param name="request"></param>
        /// <param name="serviceName"></param>
        /// <returns></returns>
        public T CallService<T>(string serviceName, object request)
        {
            return (T)CallService(serviceName, request, typeof(T));
        }

        public object CallService(string serviceName, object request, Type responseType)
        {
            var surl = BaseUrl + (serviceName == null ? "" : serviceName);
            if (BaseUrl.Contains("<<servicename>>")) surl = BaseUrl.Replace("<<servicename>>", serviceName);
            var wr = WebRequest.Create(surl) as HttpWebRequest;
            wr.Method = "POST";
            wr.ContentType = "text/json; charset=utf-8";
            if (_hdr != null)
            {
                foreach (string k in _hdr.Keys) wr.Headers.Add(k, _hdr[k]);
            };
            if (!string.IsNullOrEmpty(UserName) && !string.IsNullOrEmpty(Password) && !(_hdr != null && _hdr.ContainsKey("Authorization")))
            {
                var authInfo = Convert.ToBase64String(Encoding.Default.GetBytes(UserName + ":" + Password));
                wr.Headers.Add("Authorization", "Basic " + authInfo);
            }
            using (var s = wr.GetRequestStream())
            {
                var sw = new StreamWriter(s, Encoding.UTF8);
                GetSerializer().Serialize(sw, request);
                sw.Flush();
            }
            HttpWebResponse resp = null;
            try
            {
                resp = wr.GetResponse() as HttpWebResponse;
                using (var stm = resp.GetResponseStream())
                {
                    var enc = string.IsNullOrEmpty(resp.ContentEncoding) ? Encoding.UTF8 : Encoding.GetEncoding(resp.ContentEncoding);
                    var sr = new StreamReader(stm, enc);
                    if (responseType == null || responseType == typeof(object))
                        return GetSerializer().Deserialize(new JsonTextReader(sr));
                    else
                        return GetSerializer().Deserialize(sr, responseType);
                }
            }
            catch (WebException ex)
            {
                log.Warn("Error calling service {0}: {1}", surl, ex);
                resp = ex.Response as HttpWebResponse;
                if (resp != null)
                {
                    if (resp.ContentLength > 0)
                    {
                        using (var stm = resp.GetResponseStream())
                        {
                            var enc = string.IsNullOrEmpty(resp.ContentEncoding) ? Encoding.UTF8 : Encoding.GetEncoding(resp.ContentEncoding);
                            var sr = new StreamReader(stm, enc);
                            var msg = sr.ReadLine();
                            var sce = new ServiceClientException(msg);
                            sce.Source = BaseUrl + (serviceName == null ? "" : serviceName);
                            if (log.IsInfoEnabled) log.Info("{0}\n{1}", msg, sr.ReadToEnd());
                            throw sce;
                        }
                    }
                }
                throw;
            }    
        }

        public static IServiceClient Create(string baseUrl)
        {
            return new ServiceClient { BaseUrl = baseUrl };
        }


        public string UserName { get; set; }
        public string Password { get; set; }

        public void AddHeader(string name, string val)
        {
            if (_hdr == null) _hdr = new Dictionary<string, string>();
            _hdr.Remove(name);
            _hdr[name] = val;
        }
    }

    
    public class ServiceClientException : Exception
    {
        public ServiceClientException()
            : base()
        {
        }

        public ServiceClientException(string msg)
            : base(msg)
        {
        }

        
    }
}
