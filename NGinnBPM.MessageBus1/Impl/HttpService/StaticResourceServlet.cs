using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Reflection;
using NLog;

namespace NGinnBPM.MessageBus.Impl.HttpService
{
    /// <summary>
    /// Server for static resources
    /// </summary>
    public class StaticResourceServlet : ServletBase
    {
        public StaticResourceServlet()
        {
            RcParam = "id";
            ResponseEncoding = null;
        }
        /// <summary>
        /// Assembly where the files are stored
        /// </summary>
        public Assembly SourceAssembly { get; set; }
        /// <summary>
        /// Namespace prefix that is prepended to resource name
        /// </summary>
        public string ResourcePrefix { get; set; }
        /// <summary>
        /// Name of request parameter that contains resource id
        /// By default it's 'id'
        /// </summary>
        public string RcParam { get; set; }

        public Encoding ResponseEncoding { get; set; }

        public override  void HandleRequest(IRequestContext ctx)
        {
            string v;
            if (!ctx.UrlVariables.TryGetValue(RcParam, out v))
                v = ctx.QueryString[RcParam];
            if (v == null || v.Length == 0)
                v = "index.htm";

            string rc = ResourcePrefix + "." + v;
            log.Debug("Servlet {0} trying to load {1}", MatchUrl, rc);
            ctx.ResponseContentType = MimeTypes.GetMimeTypeForExtension(Path.GetExtension(v));
            using (Stream stm = SourceAssembly.GetManifestResourceStream(ResourcePrefix + "." + v))
            {
                if (stm == null) throw new NotFoundException();
                CopyStream(stm, ctx.OutputStream);
            }
        }
    }
}
