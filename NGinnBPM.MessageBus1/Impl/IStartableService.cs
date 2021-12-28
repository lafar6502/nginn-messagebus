using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NGinnBPM.MessageBus.Impl
{
    /// <summary>
    /// For starting and stopping services
    /// </summary>
    public interface IStartableService
    {
        void Start();
        void Stop();
        bool IsRunning { get; }
    }
}
