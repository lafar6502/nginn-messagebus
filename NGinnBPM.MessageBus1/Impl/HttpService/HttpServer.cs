using System;
using System.Text;
using System.Net;
using System.IO;
using NLog;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace NGinnBPM.MessageBus.Impl.HttpService
{
    public delegate void delReceiveWebRequest(HttpListenerContext Context);

    /// <summary>
    /// Embedded http server, provides Http interface for NGinn
    /// </summary>
    public class HttpServer : IStartableService
    {
        protected HttpListener Listener;
        protected bool IsStarted = false;
        private static Logger log = LogManager.GetCurrentClassLogger();

        public event delReceiveWebRequest ReceiveWebRequest;

        public HttpServer()
        {
        }

        /// <summary>
        /// Server listening address, use http://+:[port] to listen on all addressses
        /// </summary>
        public string ListenAddress { get; set; }

        /// <summary>
        /// Service locator used for retrieving http handlers
        /// </summary>
        public IServiceResolver ServiceLocator { get; set; }
        public MasterDispatcherServlet RootServlet { get; set; }

        public AuthenticationSchemes Authentication { get; set; }
        /// <summary>
        /// Starts the Web Service
        /// </summary>
        public void Start()
        {
            if (this.IsStarted)
                return;

            if (this.Listener == null)
            {
                this.Listener = new HttpListener();
            }

            this.Listener.Prefixes.Add(ListenAddress);
            //Listener.AuthenticationSchemes = AuthenticationSchemes.IntegratedWindowsAuthentication;
            //Listener.AuthenticationSchemeSelectorDelegate = new AuthenticationSchemeSelector(AuthSelector);
            this.IsStarted = true;
            this.Listener.Start();

            IAsyncResult result = this.Listener.BeginGetContext(new AsyncCallback(WebRequestCallback), this.Listener);
        }

        protected virtual AuthenticationSchemes AuthSelector(HttpListenerRequest rq)
        {
            log.Info("Request auth: {0}. Authenticated: {1}", rq.Url, rq.IsAuthenticated);
            return AuthenticationSchemes.IntegratedWindowsAuthentication;
        }
        /// <summary>
        /// Shut down the Web Service
        /// </summary>
        public void Stop()
        {
            if (Listener != null)
            {
                this.Listener.Close();
                this.Listener = null;
                this.IsStarted = false;
            }
        }

        public bool IsRunning
        {
            get
            {
                return this.IsStarted;
            }
        }


        protected void WebRequestCallback(IAsyncResult result)
        {
            if (this.Listener == null)
                return;

            HttpListenerContext context = this.Listener.EndGetContext(result);

            this.Listener.BeginGetContext(new AsyncCallback(WebRequestCallback), this.Listener);

            if (this.ReceiveWebRequest != null)
                this.ReceiveWebRequest(context);

            this.ProcessRequest(context);
        }

        
        /// <summary>
        /// Overridable method that can be used to implement a custom hnandler
        /// </summary>
        /// <param name="Context"></param>
        protected virtual void ProcessRequest(HttpListenerContext lsctx)
        {
            HttpListenerRequestContext ctx = new HttpListenerRequestContext(lsctx);
            log.Debug("Request: {0} {1} {2}", ctx.Url, ctx.HttpMethod, ctx.User == null ? "----" : ctx.User.Identity.Name);
            try
            {
                RequestContext.CurrentRequest = ctx;
                try
                {
                    if (!RootServlet.HandleRequest(ctx))
                    {
                        Output404(ctx);
                    }
                }
                catch (NotFoundException)
                {
                    Output404(ctx);
                }
                catch (Exception ex)
                {
                    log.Error("Request errror: {0}", ex);
                    ctx.ResponseStatus = 500;
                    ctx.ClearResponse();
                    ctx.Output.WriteLine(ex.ToString());
                }
                finally
                {
                    RequestContext.CurrentRequest = null;
                    using (var stm = lsctx.Response.OutputStream)
                    {
                        ctx.Output.Flush();
                        var st = ctx.Output.ToString();
                        var enc = ctx.ResponseEncoding == null ? Encoding.UTF8 : ctx.ResponseEncoding;
                        if (!string.IsNullOrEmpty(ctx.ResponseContentType))
                        {
                            //ctx.AddResponseHeader("Content-Type", string.Format("{0}; charset={1}", ctx.ResponseContentType, enc.WebName));
                        }
                        if (st.Length > 0)
                        {
                            var b = enc.GetBytes(st);
                            ctx.OutputStream.Write(b, 0, b.Length);
                        }

                        if (ctx.OutputStream.Length > 0)
                        {
                            var ms = (MemoryStream)ctx.OutputStream;
                            ctx.ResponseContentLength = (int) ms.Length;
                            log.Info("Response length: {0}", ctx.ResponseContentLength);
                            byte[] buf = ms.GetBuffer();
                            stm.Write(buf, 0, (int) ms.Length);
                        }
                    }
                    
                }
            }
            catch (Exception ex)
            {
                log.Error("Http handler thread error: {0}: {1}", ctx.Url, ex);
            }
        }

       

        /// <summary>
        /// Cache of servlet URL patterns
        /// </summary>
        private Dictionary<string, Regex> _urlPatterns = new Dictionary<string, Regex>();

        /// <summary>
        /// Check if specified servlet can handle the request (match the URL)
        /// </summary>
        /// <param name="rqctx"></param>
        /// <param name="srvlet"></param>
        /// <returns></returns>
        private bool CanHandleRequest(IRequestContext rqctx, string servletUrlPattern)
        {
            Regex re;
            if (!_urlPatterns.TryGetValue(servletUrlPattern, out re))
            {
                lock (_urlPatterns)
                {
                    if (!_urlPatterns.TryGetValue(servletUrlPattern, out re))
                    {
                        re = new Regex(servletUrlPattern);
                        _urlPatterns[servletUrlPattern] = re;
                    }
                }
            }
            Match m = re.Match(rqctx.RawUrl);
            if (!m.Success) return false;
            Dictionary<string, string> vars = new Dictionary<string, string>();
            for (int i = 0; i < m.Groups.Count; i++)
            {
                string name = re.GroupNameFromNumber(i);
                vars[name] = m.Groups[i].Value;
            }
            rqctx.UrlVariables = vars;
            return true;
        }

        private void Output404(IRequestContext ctx)
        {
            ctx.ResponseStatus = 404;
            ctx.Output.Write("404 not found");
        }

    }
}