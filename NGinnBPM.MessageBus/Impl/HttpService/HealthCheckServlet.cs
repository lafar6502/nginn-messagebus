using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NGinnBPM.MessageBus.Impl.HttpService
{
    [UrlPattern(@"^/health(?<service>/\w+)?")]
    public class HealthCheckServlet : ServletBase
    {
        public IServiceResolver ServiceResolver { get; set; }

        public override void HandleRequest(IRequestContext ctx)
        {
            bool b = ctx.QueryString["httpstatus"] != null;
            var srv = ctx.UrlVariables.ContainsKey("service") ? ctx.UrlVariables["service"] : null;

            foreach (var h in ServiceResolver.GetAllInstances<IHealthCheck>())
            {
                if (string.IsNullOrEmpty(srv) || srv == h.Name)
                {
                    ctx.Output.WriteLine("status.{0}={1}", h.Name, h.IsEverythingOK ? "ok" : "error");
                    ctx.Output.WriteLine("latency.{0}={1}", h.Name, (int) h.ProcessingLatency.TotalSeconds);
                    ctx.Output.WriteLine("alert.{0}={1}", h.Name, h.AlertText);
                    ctx.Output.WriteLine("lastSuccessfulRun.{0}={1}", h.Name, h.FailingSince.ToString("yyyy-MM-ddTHH:mm:ss"));
                    ctx.Output.WriteLine();

                    if (b && !h.IsEverythingOK)
                    {
                        ctx.ResponseStatus = 500;
                        break;
                    }
                }
            }

            if (!b)
            {
                ctx.Output.WriteLine("#add ?httpstatus=1 parameter to get http 500 on error");
            }
        }
    }
}
