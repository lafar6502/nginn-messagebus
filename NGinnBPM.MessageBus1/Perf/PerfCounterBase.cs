using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace NGinnBPM.MessageBus.Perf
{
    /// <summary>
    /// base functionality for all perf counters
    /// </summary>
    public abstract class PerfCounterBase : IPerfCounter
    {
        public abstract double GetValue();
        /// <summary>
        /// Regular expression to check for matching log messages
        /// </summary>
        public Regex MatchMessageRE { get; set; }
        /// <summary>
        /// Regular expression for extracting the numeric value from log message
        /// </summary>
        public Regex ExtractValueRE { get; set; }
        /// <summary>
        /// Perf counter name
        /// </summary>
        public string Name { get; set; }

        public virtual bool HandleEvent(DateTime timestamp, string message)
        {
            if (MatchMessageRE != null)
                if (!MatchMessageRE.IsMatch(message))
                {
                    return false;
                }
            HandleMatchingMessage(timestamp, message);
            return true;
        }

        protected virtual string ExtractValue(string message, string defVal)
        {
            Match m = ExtractValueRE.Match(message);
            if (!m.Success) return defVal;
            return m.Groups[1].Value;
        }

        protected abstract void HandleMatchingMessage(DateTime tstamp, string message);

        public virtual void OnTimer()
        {
        }
    }
}
