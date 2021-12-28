using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Timers;
using NLog;


namespace NGinnBPM.MessageBus.Perf
{
    /// <summary>
    /// Performance counter set.
    /// </summary>
    public class PerfCounterSet : IPerfCounterSet, IDisposable, System.ComponentModel.ISupportInitialize
    {
        private Dictionary<string, IPerfCounter> _counters = new Dictionary<string, IPerfCounter>();
        public bool AutoLogStatistics { get; set; }
        private Logger log = LogManager.GetCurrentClassLogger();

        public PerfCounterSet()
        {
        }

        private string _name;
        public string Name {
            get
            {
                return _name;
            }
            set
            {
                _name = value;
                log = LogManager.GetLogger("PerfCounterSet." + _name);
            }
        }

        /// <summary>
        /// Service locator for resolving
        /// counter instances
        /// </summary>
        public IServiceResolver ServiceLocator { get; set; }

        private void LogStats()
        {
            if ( _counters.Count > 0)
            {
                StringBuilder sb = new StringBuilder();
                List<IPerfCounter> l = new List<IPerfCounter>(_counters.Values);
                foreach (IPerfCounter pcb in l)
                {
                    sb.AppendLine(); 
                    sb.Append(pcb.Name);
                    sb.Append(":");
                    sb.Append(pcb.GetValue());
                }
                log.Info(sb.ToString());
            }
        }

        #region IPerfCounterSet Members

        public double GetValue(string counterName)
        {
            IPerfCounter pc;
            if (_counters.TryGetValue(counterName, out pc))
                return pc.GetValue();
            if (Debug) log.Warn("Counter not found: {0}", counterName);
            return 0.0;
        }

        public IList<string> GetCounterNames()
        {
            return new List<string>(_counters.Keys);
        }

        #endregion

        #region IPerfCounterSet Members


        public void NotifyEvent(DateTime timeStamp, string message)
        {
            List<IPerfCounter> l = new List<IPerfCounter>(_counters.Values);
            bool b = false;
            foreach (IPerfCounter pcb in l)
            {
                if (pcb.HandleEvent(timeStamp, message)) b = true;
            }
            if (Debug)
            {
                if (!b) log.Info("Unhandled event: {0}", message);
            }
        }

        #endregion

        internal void AddCounter(IPerfCounter pcb)
        {
            if (_counters.ContainsKey(pcb.Name)) throw new Exception("Already defined: " + pcb.Name);
            _counters[pcb.Name] = pcb;
        }

        #region IDisposable Members

        public void Dispose()
        {
        }

        #endregion

        public bool Debug { get; set; }

        #region ISupportInitialize Members

        public void BeginInit()
        {
        }

        public void EndInit()
        {
            if (ServiceLocator != null)
            {
                foreach (IPerfCounter pc in ServiceLocator.GetAllInstances<IPerfCounter>())
                {
                    AddCounter(pc);
                    ServiceLocator.ReleaseInstance(pc);
                }
            }
        }

        #endregion
    }
}
