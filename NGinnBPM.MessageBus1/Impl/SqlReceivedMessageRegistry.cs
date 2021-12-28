using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;

namespace NGinnBPM.MessageBus.Impl
{
    /// <summary>
    /// TODO: implement
    /// </summary>
    public class SqlReceivedMessageRegistry : IReceivedMessageRegistry, System.ComponentModel.ISupportInitialize
    {
        public SqlReceivedMessageRegistry()
        {
            MaxMessages = 1000000;
        }

        private HashSet<string> _ids = new HashSet<string>();

        #region IReceivedMessageRegistry Members

        public bool HasBeenReceived(string id)
        {
            return _ids.Contains(id);
        }

        public void RegisterReceived(string id)
        {
            _ids.Add(id);
        }

        #endregion

        #region ISupportInitialize Members

        public void BeginInit()
        {
            Start();
        }

        public void EndInit()
        {
        }

        #endregion

        public string ConnectionString { get; set; }
        public string QueueTableName { get; set; }
        public int MaxMessages { get; set; }

        public void Start()
        {
            ReadIdsFromDatabase();
        }

        protected void ReadIdsFromDatabase()
        {
            string sql = "select top(1000000) unique_id from {0} with(nolock) where insert_time >= @max_date order by id desc";

        }
    }
}
