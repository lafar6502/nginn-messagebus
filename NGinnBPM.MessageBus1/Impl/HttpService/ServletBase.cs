using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using NLog;

namespace NGinnBPM.MessageBus.Impl.HttpService
{
    public abstract class ServletBase : IServlet
    {
        public string MatchUrl {get;set;}
        protected Logger log;

        public ServletBase()
        {
            log = LogManager.GetCurrentClassLogger();
        }

        protected static void CopyStream(Stream input, Stream output)
        {
            var buf = new byte[20000];
            int n;
            while ((n = input.Read(buf, 0, buf.Length)) > 0) output.Write(buf, 0, n);
        }


        public abstract void HandleRequest(IRequestContext ctx);
    }
}
