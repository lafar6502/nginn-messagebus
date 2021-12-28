using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NGinnBPM.MessageBus.Impl.HttpService;
using System.Net;
namespace NGinnBPM.MessageBus.Perf
{
    [UrlPattern(@"/perf/(?<inst>.+)?")]
    public class PerfServlet : IServlet
    {
        public string MatchUrl { get; set; }

        public void HandleRequest(IRequestContext ctx)
        {
            string cnt = ctx.QueryString["counter"];
            string inst = ctx.UrlVariables["inst"];
            if (inst != null && inst.Length > 0)
            {
                ctx.Output.Write(DefaultCounters.PerfCounters.GetValue(inst));
            }
            else
            {
                ctx.ResponseContentType = "text/html";
                ctx.Output.WriteLine("Available perf counters: <br/>");
                ctx.Output.WriteLine("<ol>");
                foreach (string name in DefaultCounters.PerfCounters.GetCounterNames())
                {
                    ctx.Output.WriteLine("<li><a href=\"./{0}\">{0}</a></li>", name);
                }
                ctx.Output.WriteLine("</ol>");
            }
        }


        
    }
}
