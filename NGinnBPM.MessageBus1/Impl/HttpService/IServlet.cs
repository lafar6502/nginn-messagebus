using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;

namespace NGinnBPM.MessageBus.Impl.HttpService
{
    /// <summary>
    /// Servlet interface
    /// </summary>
    public interface IServlet
    {
        /// <summary>
        /// URL matching regular expression
        /// </summary>
        string MatchUrl { get; set; }
        /// <summary>
        /// Handle the request
        /// </summary>
        /// <param name="ctx"></param>
        void HandleRequest(IRequestContext ctx);
    }
}
