using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NGinnBPM.MessageBus.Perf
{
    /// <summary>
    /// Remembers last value retrieved from log event
    /// </summary>
    class LastValueCalculator : PerfCounterBase
    {
        double _lastVal = 0;
        public override double GetValue()
        {
            return _lastVal;
        }

        protected override void HandleMatchingMessage(DateTime tstamp, string message)
        {
            _lastVal = Double.Parse(ExtractValue(message, "0"));
        }
    }
}
