using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.Data.SqlTypes;
using System.Data.Common;

namespace NGinnBPM.MessageBus.Impl
{
    public class SqlUtil
    {
        
        
        
        /// <summary>
        /// Parse the queue endpoint
        /// </summary>
        /// <param name="endpoint"></param>
        /// <param name="connection"></param>
        /// <param name="qTable"></param>
        /// <param name="endpoint"></param>
        /// <returns></returns>
        public static bool ParseSqlEndpoint(string endpoint, out string connection, out string qTable)
        {
            return ParseSqlEndpoint(endpoint, out connection, out qTable, out _);
        }

        /// <summary>
        /// Extended version of ParseSqlEndpoint
        /// recognizing urls like
        /// sql://testdb/MQ_Polled/ClientId
        /// for creating polled message queues
        /// </summary>
        /// <param name="endpoint"></param>
        /// <param name="connection"></param>
        /// <param name="qTable"></param>
        /// <param name="clientId"></param>
        /// <returns></returns>
        public static bool ParseSqlEndpoint(string endpoint, out string connection, out string qTable, out string clientId)
        {
            connection = null;
            qTable = null;
            clientId = null;
            if (!endpoint.StartsWith("sql://"))
                return false;
            endpoint = endpoint.Substring(6);
            var i1 = endpoint.IndexOf('/');
            if (i1 <= 0) return false;
            var i2 = endpoint.IndexOf('/', i1 + 1);
            connection = endpoint.Substring(0, i1);
            qTable = endpoint.Substring(i1 + 1);
            if (i2 >= 0)
            {
                qTable = endpoint.Substring(i1 + 1, i2 - i1 - 1);
                clientId = endpoint.Substring(i2 + 1);
            }
            return true;
        }
    }
}
