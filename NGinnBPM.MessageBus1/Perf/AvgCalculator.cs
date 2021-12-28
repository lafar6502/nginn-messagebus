using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NGinnBPM.MessageBus.Perf
{
    /// <summary>
    /// Calculates an arithmetic average of last N samples
    /// </summary>
    public class AvgCalculator : PerfCounterBase
    {
        Queue<double> _q = new Queue<double>();

        int NumSamples { get; set; }

        public AvgCalculator()
        {
            NumSamples = 100;
        }

        public override double GetValue()
        {
            return _q.Average();
        }

        protected override void HandleMatchingMessage(DateTime tstamp, string message)
        {
            string s = ExtractValue(message, null);
            double d2 = Double.Parse(s);

            while (_q.Count >= NumSamples)
                _q.Dequeue();
            _q.Enqueue(d2);
        }
    }
}
