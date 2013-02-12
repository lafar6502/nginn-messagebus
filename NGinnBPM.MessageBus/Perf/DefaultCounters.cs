using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace NGinnBPM.MessageBus.Perf
{
    /// <summary>
    /// Set of global performance counters.
    /// </summary>
    public class DefaultCounters
    {
        static DefaultCounters()
        {
            PerfCounterSet pcs = new PerfCounterSet { Name = "Global", AutoLogStatistics = true };
            pcs.AddCounter(new PercentileCalculator { MatchMessageRE = new Regex("STAT.*"), ExtractValueRE = new Regex(""), Name = "ALL" });
            _pcs = pcs;

        }
        private static IPerfCounterSet _pcs;
        public static IPerfCounterSet PerfCounters
        {
            get
            {
                return _pcs;
            }
        }


        public static void ConfigureFromFile(string file)
        {

            XmlPerfCounterSetBuilder b = new XmlPerfCounterSetBuilder();
            IPerfCounterSet pcs = b.BuildFromFile(file);
            IDisposable d = _pcs as IDisposable;
            if (d != null) d.Dispose();
            _pcs = pcs;
        }

        public static void SetGlobalPerfCounters(IPerfCounterSet counterSet)
        {
            _pcs = counterSet;
        }

        public static void LogMessage(string timeStamp, string message)
        {
            PerfCounters.NotifyEvent(DateTime.Parse(timeStamp), message);
        }
    }
}
