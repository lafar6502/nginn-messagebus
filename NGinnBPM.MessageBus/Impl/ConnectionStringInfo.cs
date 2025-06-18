using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NGinnBPM.MessageBus.Impl
{
    /// <summary>
    /// a replacement class for ConnectionStringSettings from System.Data.SqlClient
    /// </summary>
    public class ConnectionStringInfo
    {
        public string Alias { get; set; }
        public string ConnectionString { get; set; }
        public string ProviderName { get; set; }

    }
}
