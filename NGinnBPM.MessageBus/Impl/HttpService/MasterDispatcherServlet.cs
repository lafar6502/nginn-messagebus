using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace NGinnBPM.MessageBus.Impl.HttpService
{
    public class MasterDispatcherServlet 
    {
        public string RootUrl {get;set;}

        public IServiceResolver ServiceResolver { get; set; }

        public virtual bool HandleRequest(IRequestContext ctx)
        {
            ctx.DispatchUrl = ctx.RawUrl;
            if (!string.IsNullOrEmpty(RootUrl))
            {
                var idx = ctx.RawUrl.IndexOf(RootUrl);
                if (idx < 0)
                {
                    return false;
                }
                ctx.DispatchUrl = ctx.RawUrl.Substring(idx);
            }
            foreach (IServlet srvlet in ServiceResolver.GetAllInstances<IServlet>())
            {
                if (CanHandleRequest(ctx, srvlet.MatchUrl))
                {
                    srvlet.HandleRequest(ctx);
                    return true;
                }
            }
            return false;
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
            Match m = re.Match(rqctx.DispatchUrl);
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
    }
}
