using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Security.Principal;
using System.IO;
using System.Collections.Specialized;

namespace NGinnBPM.MessageBus.Impl.HttpService
{
    /// <summary>
    /// TODO
    /// </summary>
    public interface IRequestContext
    {
        Encoding RequestEncoding { get; }
        Encoding ResponseEncoding { get; set; }
        int RequestContentLength { get; }
        int ResponseContentLength { get; set; }
        IPrincipal User { get; }
        IDictionary<string, string> UrlVariables { get; set; }
        NameValueCollection QueryString { get; }
        NameValueCollection Headers { get; }
        string RequestContentType { get; }
        string ResponseContentType { get; set; }
        string HttpMethod { get; }
        Uri Url { get; }
        string RawUrl { get; }
        string DispatchUrl { get; set; }
        string ClientIP { get; }
        Stream InputStream { get; }
        Stream OutputStream { get; }
        TextReader Input { get; }
        TextWriter Output { get; }
        int ResponseStatus { get; set; }
        void ClearResponse();
        void AddResponseHeader(string name, string value);
    }

    public class RequestContext
    {
        [ThreadStatic]
        private static IRequestContext _cur;

        public static IRequestContext CurrentRequest
        {
            get { return _cur; }
            set { _cur = value; }
        }
    }


}
