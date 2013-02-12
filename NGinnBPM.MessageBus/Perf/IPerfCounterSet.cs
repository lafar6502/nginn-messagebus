using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NGinnBPM.MessageBus.Perf
{
    /// <summary>
    /// Set of performance counters.
    /// </summary>
    public interface IPerfCounterSet
    {
        /// <summary>
        /// Get current value of a performance counter
        /// </summary>
        /// <param name="counterName"></param>
        /// <returns></returns>
        double GetValue(string counterName);
        /// <summary>
        /// Get list of names of performance counters in this set
        /// </summary>
        /// <returns></returns>
        IList<string> GetCounterNames();
        /// <summary>
        /// Notify performance counters about an event.
        /// Event is represented by a log message (string) 
        /// that is parsed by perf counters
        /// </summary>
        /// <param name="timeStamp"></param>
        /// <param name="message"></param>
        void NotifyEvent(DateTime timeStamp, string message);
    }
}
