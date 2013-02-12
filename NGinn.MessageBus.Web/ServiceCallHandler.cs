using System;
using System.Web;
using NLog;
using NGinnBPM.MessageBus.Impl.HttpService;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NGinnBPM.MessageBus;

namespace NGinn.MessageBus.Web
{
    public class ServiceCallHandler : IHttpHandler
    {
        private Logger log = LogManager.GetCurrentClassLogger();
        private Regex _re = new Regex(@"/ws/call/(?<name>\w+)?");
        /// <summary>
        /// You will need to configure this handler in the web.config file of your 
        /// web and register it with IIS before being able to use it. For more information
        /// see the following link: http://go.microsoft.com/?linkid=8101007
        /// </summary>
        #region IHttpHandler Members

        public bool IsReusable
        {
            // Return false in case your Managed Handler cannot be reused for another request.
            // Usually this would be false in case you have some state information preserved per request.
            get { return true; }
        }

        public void ProcessRequest(HttpContext context)
        {
            if (GlobalServiceResolver == null) throw new Exception("GlobalServiceResolver not set");

            context.Response.Buffer = true;
            var w = new AspnetRequestWrapper(context);
            //context.Response.Output.WriteLine("Dis is {0}", context.Request.RawUrl);
            //write your handler implementation here.

            try
            {
                var dispatcher = GlobalServiceResolver.GetInstance<MasterDispatcherServlet>();
                if (!dispatcher.HandleRequest(w))
                {
                    log.Info("Master dispatcher did not handle request {0}", context.Request.Url);
                }
            }
            catch (Exception ex)
            {
                w.ClearResponse();
                w.ResponseStatus = 500;
                w.Output.WriteLine("Error: {0}", ex.Message);
            }
            finally
            {
            }
        }

        #endregion

        public static IServiceResolver GlobalServiceResolver { get; set; }
    }
}
