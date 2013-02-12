using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using NGinnBPM.MessageBus.Impl.HttpService;

namespace Tests
{
[UrlPattern(@"^/hello/")]
public class HelloWorldServlet : ServletBase
{
    public override void HandleRequest(IRequestContext ctx)
    {
        ctx.Output.WriteLine("Hello, world");
    }
}
}
