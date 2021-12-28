using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NGinnBPM.MessageBus.Perf
{
    /// <summary>
    /// Calculates number of event occurences
    /// </summary>
    class CountCalculator : PerfCounterBase
    {
        int _cnt = 0;

        public override double GetValue()
        {
            return _cnt;
        }

        protected override void HandleMatchingMessage(DateTime tstamp, string message)
        {
            System.Threading.Interlocked.Increment(ref _cnt);
        }
    }
}
