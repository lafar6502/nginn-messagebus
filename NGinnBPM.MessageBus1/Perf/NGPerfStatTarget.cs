using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NLog;
using NLog.Targets;



namespace NGinnBPM.MessageBus.Perf
{

    [Target("NGPerfCounters")]
    class NGPerfStatTarget : TargetWithLayout
    {
        protected IPerfCounterSet CounterSet { get; set; }

        public NGPerfStatTarget()
        {
            CounterSet = DefaultCounters.PerfCounters;
        }

        protected override void Write(LogEventInfo logEvent)
        {
            CounterSet.NotifyEvent(logEvent.TimeStamp, logEvent.Message);
        }
    }
}
