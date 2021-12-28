using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security.Principal;

namespace NGinnBPM.MessageBus.Impl
{
    /// <summary>
    /// Current context of service call.
    /// Used for passing custom http headers to the handler.
    /// </summary>
    public class ServiceCallContext
    {
        [ThreadStatic]
        private static ServiceCallContext _cur;

        public static ServiceCallContext Current 
        {
            get { return _cur; }
            set { _cur = value; }
        }

        public IDictionary<string, string> Headers { get; set; }
        public IPrincipal User { get; set; }
    }
}
