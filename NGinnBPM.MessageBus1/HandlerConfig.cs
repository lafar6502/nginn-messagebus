using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NGinnBPM.MessageBus
{
    /// <summary>
    /// Optional attribute supplying additional configuration options for message handler types
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class MessageHandlerConfigAttribute : System.Attribute
    {
        /// <summary>
        /// set to true to register handler as a transient instance,
        /// false to register it as a singleton
        /// </summary>
        public bool Transient { get; set; }
    }
}
