using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NGinnBPM.MessageBus.Impl
{
    /// <summary>
    /// Health monitoring interface, used 
    /// for http health monitoring
    /// </summary>
    public interface IHealthCheck
    {
        string Name { get; }
        /// <summary>
        /// Check if everything is ok
        /// </summary>
        bool IsEverythingOK { get; }
        /// <summary>
        /// Error information
        /// </summary>
        string AlertText { get;}
        /// <summary>
        /// Date of the failure occurring
        /// </summary>
        DateTime FailingSince { get; }
        /// <summary>
        /// Latency time, for monitoring processing latency
        /// If not applicable service returns Timespan.Zero
        /// </summary>
        TimeSpan ProcessingLatency { get; }
    }
}
