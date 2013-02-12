using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using NLog;

namespace NGinnBPM.MessageBus.Impl.HttpService
{
    /// <summary>
    /// serves content from a directory
    /// </summary>
    public class FSDirectoryServlet : ServletBase
    {
        
        public FSDirectoryServlet()
        {
            RcParam = "id";
            ResponseEncoding = null;
        }
        /// <summary>
        /// Base directory location
        /// </summary>
        public string BaseDirectory { get; set; }

        /// <summary>
        /// Name of request parameter (either URl variable or query string param)
        /// that contains the file  name. By default it's 'id'
        /// </summary>
        public string RcParam { get; set; }

        /// <summary>
        /// Encoding used when serving files. Sorry, all files are expected to have same encoding
        /// </summary>
        public Encoding ResponseEncoding { get; set; }

        public override void HandleRequest(IRequestContext ctx)
        {
            string v;
            if (!ctx.UrlVariables.TryGetValue(RcParam, out v))
                v = ctx.QueryString[RcParam];
            if (v == null || v.Length == 0)
                v = "index.htm";
            ctx.ResponseContentType = MimeTypes.GetMimeTypeForExtension(Path.GetExtension(v));
            
            string pth = Path.GetFullPath(Path.Combine(BaseDirectory, v));
            if (Path.GetDirectoryName(pth).Length < BaseDirectory.Length)
            {
                log.Warn("Trying to get content above the root dir: {0}", pth);
                throw new NotFoundException();
            }
            if (!File.Exists(pth)) throw new NotFoundException();

            FileInfo fi = new FileInfo(pth);
            ctx.ResponseContentLength = (int) fi.Length;
            
            using (Stream stm = fi.OpenRead())
            {
                CopyStream(stm, ctx.OutputStream);
            }

            
        }


    }
}
