using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.Common;

namespace NGinnBPM.MessageBus.Impl.InternalEvents
{
    /// <summary>
    /// This event will be published during message bus startup if we're using a database
    /// so various initialization scripts can be run.
    /// </summary>
    public class DatabaseInit
    {
        public DbConnection Connection { get; set; }
    }
}
