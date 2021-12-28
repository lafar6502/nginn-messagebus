using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;
using System.Collections.Specialized;
using NLog;

namespace NGinnBPM.MessageBus.Impl.HttpService
{
    

    public class HttpListenerRequestContext : IRequestContext
    {
        private HttpListenerContext _ctx;
        private MemoryStream _outStm = new MemoryStream();
        private StringWriter _out = new StringWriter();
        private TextReader _input;

        public HttpListenerRequestContext(HttpListenerContext ctx)
        {
            _ctx = ctx;
            _input = new StreamReader(_ctx.Request.InputStream, _ctx.Request.ContentEncoding);
            DispatchUrl = _ctx.Request.RawUrl;
        }

        public Encoding RequestEncoding
        {
            get { return _ctx.Request.ContentEncoding; }
        }

        public Encoding ResponseEncoding
        {
            get
            {
                return _ctx.Response.ContentEncoding;
            }
            set
            {
                _ctx.Response.ContentEncoding = value;
            }
        }

        public string ClientIP
        {
            get
            {
                return _ctx.Request.UserHostAddress;
            }
        }

        public int ResponseContentLength
        {
            get
            {
                return (int) _ctx.Response.ContentLength64;
            }
            set
            {
                _ctx.Response.ContentLength64 = value;
            }
        }

        public System.Security.Principal.IPrincipal User
        {
            get { return _ctx.User; }
        }

        public IDictionary<string, string> UrlVariables { get;set;}


        /// <summary>
        /// Manual parsing because of Microsoft bug (invalid character encoding used for parsing the url query string)
        /// </summary>
        public NameValueCollection QueryString
        {
            get 
            {
                return ParseQueryString(RawUrl);
            }
        }

        public static NameValueCollection ParseQueryString(string qs)
        {
            NameValueCollection nvc = new NameValueCollection();
            int idx = qs.IndexOf('?');
            if (idx <= 0) return nvc;
            qs = qs.Substring(idx + 1);
            foreach (string pt in qs.Split('&'))
            {
                int idx2 = pt.IndexOf('=');
                string n = idx2 >= 0 ? pt.Substring(0, idx2) : pt;
                string v = idx2 >= 0 ? pt.Substring(idx2 + 1) : "";
                nvc[Uri.UnescapeDataString(n)] = Uri.UnescapeDataString(v);
            }
            return nvc;
        }

        public string RequestContentType
        {
            get { return _ctx.Request.ContentType; }
        }

        public string ResponseContentType
        {
            get
            {
                return _ctx.Response.ContentType;
            }
            set
            {
                _ctx.Response.ContentType = value;
            }
        }

        public string HttpMethod
        {
            get
            {
                return _ctx.Request.HttpMethod;
            }
            
        }

        public Uri Url
        {
            get { return _ctx.Request.Url; }
        }

        public System.IO.Stream InputStream
        {
            get { return _ctx.Request.InputStream; }
        }

        public System.IO.Stream OutputStream
        {
            get { return _outStm;  }
        }

        public System.IO.TextReader Input
        {
            get { return _input; }
        }

        public System.IO.TextWriter Output
        {
            get { return _out; }
        }


        public int RequestContentLength
        {
            get { return (int) _ctx.Request.ContentLength64; }
        }


        public int ResponseStatus
        {
            get
            {
                return _ctx.Response.StatusCode;
            }
            set
            {
                _ctx.Response.StatusCode = value;
            }
        }

        public void ClearResponse()
        {
            _out = new StringWriter();
            _outStm = new MemoryStream();
        }


        public string RawUrl
        {
            get { return _ctx.Request.RawUrl; }
        }

        public string DispatchUrl { get; set; }


        public NameValueCollection Headers
        {
            get { return _ctx.Request.Headers; }
        }

        public void AddResponseHeader(string name, string value)
        {
            _ctx.Response.AddHeader(name, value);
        }
    }
}
