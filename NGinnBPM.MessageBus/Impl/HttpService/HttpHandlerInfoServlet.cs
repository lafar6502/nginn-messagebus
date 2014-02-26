using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NGinnBPM.MessageBus.Impl.HttpService
{
    [UrlPattern(@"^/__httphandlers/$")]
    public class HttpHandlerInfoServlet : ServletBase
    {
        public IServiceResolver ServiceResolver { get; set; }

        public override void HandleRequest(IRequestContext ctx)
        {
            foreach (var s in ServiceResolver.GetAllInstances<IServlet>())
            {
                ctx.Output.WriteLine("{0} :: {1}", s.MatchUrl, s.GetType().Name);
                ServiceResolver.ReleaseInstance(s);
            }
        }
    }
}
