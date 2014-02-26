using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NLog;
using NLog.Targets;
using Newtonsoft.Json;
using System.IO;

namespace NGinnBPM.MessageBus.Impl.HttpService
{
    [UrlPattern(@"^/call/(?<name>\w+)?")]
    public class ServiceCallServlet : ServletBase
    {
        protected IServiceMessageDispatcher ServiceDispatcher { get; set; }
        protected JsonServiceCallHandler CallHandler { get; set; }

        public ServiceCallServlet(IServiceMessageDispatcher dispatcher, JsonServiceCallHandler jsonCallHandler)
        {
            ServiceDispatcher = dispatcher;
            CallHandler = jsonCallHandler;
        }

        public override  void HandleRequest(IRequestContext ctx)
        {
            var h = new Dictionary<string, string>();
            foreach (string k in ctx.QueryString)
                h[k] = ctx.QueryString[k];
            foreach (string k in ctx.Headers)
                h[k] = ctx.Headers[k];
            
            ServiceCallContext.Current = new ServiceCallContext
            {
                Headers = h,
                User = ctx.User
            };
            try
            {
                if (ctx.HttpMethod == "GET")
                {
                    foreach (var si in ServiceDispatcher.Services)
                    {
                        ctx.Output.WriteLine("{0} : {1}", si.Name, si.RequestType.FullName);
                    }
                }
                else if (ctx.HttpMethod == "POST" || ctx.HttpMethod == "PUT")
                {
                    string sname = ctx.UrlVariables["name"];
                    CallHandler.HandleServiceCall(sname, ctx.RequestContentType, ctx.Input, ctx.Output);
                }
            }
            finally
            {
                ServiceCallContext.Current = null;
            }
        }
    
        
    }
}
