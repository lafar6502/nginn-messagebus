using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NGinnBPM.MessageBus.Perf
{
    /// <summary>
    /// Percentile calculator - calculates a percentile from last N samples.
    /// </summary>
    class PercentileCalculator : PerfCounterBase
    {
        Queue<double> _q = new Queue<double>();
        /// <summary>
        /// Number of samples to use for calculations
        /// </summary>
        public int NumSamples { get; set; }
        /// <summary>
        /// Percentile to calculate (1..100)
        /// </summary>
        public double Percentile { get; set; }
        public PercentileCalculator()
        {
            NumSamples = 300;
            Percentile = 97.0;
        }

        public override double GetValue()
        {
            if (_q.Count == 0) return 0.0;
            double[] v = _q.ToArray();
            Array.Sort(v);
            int idx = (int) Math.Round(Percentile * (v.Length - 1)/ 100.0);
            return v[idx];
        }

        protected override void HandleMatchingMessage(DateTime tstamp, string message)
        {
            while (_q.Count >= NumSamples)
                _q.Dequeue();
            double d = Double.Parse(ExtractValue(message, "0"));
            _q.Enqueue(d);
        }
    }
}
