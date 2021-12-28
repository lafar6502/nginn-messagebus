using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NGinnBPM.MessageBus.Perf
{
    /// <summary>
    /// Performance counter interface
    /// </summary>
    public interface IPerfCounter
    {
        string Name { get; }
        double GetValue();
        bool HandleEvent(DateTime timestamp, string message);
    }
}
