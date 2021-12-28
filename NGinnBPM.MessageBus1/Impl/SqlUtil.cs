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
            connection = null;
            qTable = null;
            if (!endpoint.StartsWith("sql://"))
                return false;
            endpoint = endpoint.Substring(6);
            string[] ss = endpoint.Split('/');
            if (ss.Length != 2)
                return false;
            connection = ss[0];
            qTable = ss[1];
            return true;
        }

    }
}
