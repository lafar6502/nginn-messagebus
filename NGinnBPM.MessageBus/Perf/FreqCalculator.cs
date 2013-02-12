using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NGinnBPM.MessageBus.Perf
{
    /// <summary>
    /// Calculates a frequency of event occurrence
    /// </summary>
    class FreqCalculator : PerfCounterBase
    {
        private Queue<DateTime> _eventQ = new Queue<DateTime>();
        /// <summary>
        /// Number of last events to analyze
        /// </summary>
        public int NumEvents { get; set; }
        /// <summary>
        /// Max time period for calculation
        /// </summary>
        public int TimePeriodSecs { get; set; }

        public FreqCalculator()
        {
            TimePeriodSecs = 60;
            NumEvents = 50;
        }

        public override double GetValue()
        {
            DateTime dt = DateTime.Now.AddSeconds(-TimePeriodSecs);
            while (_eventQ.Count > 0 && _eventQ.Peek() < dt)
                _eventQ.Dequeue();
            if (_eventQ.Count == 0)
                return 0.0;
            DateTime lastEvent = _eventQ.Peek();
            double secs = (DateTime.Now - lastEvent).TotalSeconds;
            return _eventQ.Count/ secs;
        }

        protected override void HandleMatchingMessage(DateTime tstamp, string message)
        {
            while (_eventQ.Count > NumEvents)
                _eventQ.Dequeue();
            _eventQ.Enqueue(tstamp);
        }

        public override void OnTimer()
        {
        }
    }
}
