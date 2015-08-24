using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.ComponentModel;
using System.Data.Common;
using NLog;
using Newtonsoft.Json;
using NGinnBPM.MessageBus.Impl.SqlQueue;
using System.Configuration;

namespace NGinnBPM.MessageBus.Impl
{
    /// <summary>
    /// This class represents current state of a single sequence of messages.
    /// </summary>
    public class SequenceInfo
    {
        public string Id { get; set; }
        /// <summary>
        /// Number of last handled message in this sequence
        /// </summary>
        public int? LastProcessedNumber { get; set; }
        /// <summary>
        /// Map message Id-> message number in sequence, used for tracking remaining sequence messages.
        /// </summary>
        public Dictionary<int, string> RemainingMessages { get; set; }
        /// <summary>
        /// Date when last message belonging to this sequence has been 'seen'
        /// </summary>
        public DateTime LastMessageSeen { get; set; }
        /// <summary>
        /// sequence length
        /// </summary>
        public int? Length { get; set; }
    }

    public class SqlSequenceManager : ISequenceMessages, IMessageConsumer<InternalEvents.DatabaseInit>
    {
        public SqlSequenceManager()
        {
            SequenceTable = "NGMB_SequenceInfo";
        }

        public string SequenceTable { get; set; }
        public bool AutoCreateTable { get; set; }
        
        public int CacheSize { get; set; }

        /// <summary>Ids of currently processed sequences</summary>
        private HashSet<string> _currentlyProcessed = new HashSet<string>();

        //private SimpleCache<string, SequenceInfo> _cache = new SimpleCache<string,SequenceInfo>();

        private object _lck = new object();
        private Logger log = LogManager.GetCurrentClassLogger();

        public void BeginInit()
        {
            
        }

        public void EndInit()
        {
            
        }

        protected void UpdateSequence(string id, DbConnection conn, Action<SequenceInfo> act)
        {
            bool found = false;
            SequenceInfo si = null;
            var sql = SqlQueue.SqlHelper.GetSqlAbstraction(conn);
            using (var cmd = sql.CreateCommand(conn))
            {
                cmd.CommandText = string.Format(SqlHelper.GetNamedSqlQuery("SqlSequenceManager_SelectWithLock", sql.Dialect), SequenceTable);
                sql.AddParameter(cmd, "id", id);
                using (var rdr = cmd.ExecuteReader())
                {
                    if (rdr.Read())
                    {
                        found = true;
                        si = JsonConvert.DeserializeObject<SequenceInfo>(rdr.GetString(0));
                    }
                    else
                    {
                        si = new SequenceInfo {
                            Id = id,
                            RemainingMessages = new Dictionary<int,string>(),
                            LastMessageSeen = DateTime.Now
                        };
                    }
                }
                act(si);
                si.LastMessageSeen = DateTime.Now;
                if (found)
                {
                    cmd.CommandText = SqlHelper.FormatSqlQuery("SqlSequenceManager_Update", sql.Dialect, SequenceTable);
                }
                else
                {
                    cmd.CommandText = SqlHelper.FormatSqlQuery("SqlSequenceManager_Insert", sql.Dialect, SequenceTable);
                }
                if (si.Length.HasValue && si.LastProcessedNumber.HasValue && si.LastProcessedNumber.Value >= si.Length.Value - 1)
                {
                    log.Info("Sequence completed: {0}", si.Id);
                    if (found)
                    {
                        cmd.CommandText = SqlHelper.FormatSqlQuery("SqlSequenceManager_Delete", sql.Dialect, SequenceTable);
                    }
                    else
                    {
                        return; //nothing to do
                    }
                }
                cmd.Parameters.Clear();
                sql.AddParameter(cmd, "json", JsonConvert.SerializeObject(si));
                sql.AddParameter(cmd, "mdate", DateTime.Now);
                sql.AddParameter(cmd, "id", id);
                int n = cmd.ExecuteNonQuery();
                if (n == 0) throw new Exception("Unexpected error - no records were updated");
            }
        }
        /// <summary>
        /// get the sequence info for specified seq
        /// updates the sequence table only if the sequence message will be processed now
        /// </summary>
        /// <param name="seqId"></param>
        /// <param name="seqNum">0-based number of message in sequence</param>
        /// <param name="tran"></param>
        /// <param name="act"></param>
        private SequenceMessageDisposition UpdateSequenceInfo(string seqId, int seqNum, int? seqLen, string msgId, DbConnection con)
        {
            if (string.IsNullOrEmpty(seqId)) throw new ArgumentException("seqId");
            if (seqNum < 0) throw new ArgumentException("seqNum");

            var md = new SequenceMessageDisposition();
            md.MessageDispositon = SequenceMessageDisposition.ProcessingDisposition.HandleMessage;
            
            UpdateSequence(seqId, con, si => {
                if (seqLen.HasValue) si.Length = seqLen;
                if (si.LastProcessedNumber.HasValue)
                {
                    if (seqNum == si.LastProcessedNumber.Value + 1)
                    {
                        md.MessageDispositon = SequenceMessageDisposition.ProcessingDisposition.HandleMessage;
                        si.LastProcessedNumber = seqNum;
                    }
                    else if (seqNum <= si.LastProcessedNumber.Value)
                    {
                        log.Warn("Duplicate sequence message {1} in sequence {0}", seqId, msgId);
                        md.MessageDispositon = SequenceMessageDisposition.ProcessingDisposition.HandleMessage;
                    }
                    else
                    {
                        md.MessageDispositon = SequenceMessageDisposition.ProcessingDisposition.Postpone;
                        if (!string.IsNullOrEmpty(msgId)) si.RemainingMessages[seqNum] = msgId;
                    }
                }
                else
                {
                    if (seqNum == 0)
                    {
                        md.MessageDispositon = SequenceMessageDisposition.ProcessingDisposition.HandleMessage;
                        si.LastProcessedNumber = seqNum;
                    }
                    else
                    {
                        md.MessageDispositon = SequenceMessageDisposition.ProcessingDisposition.Postpone;
                        if (!string.IsNullOrEmpty(msgId)) si.RemainingMessages[seqNum] = msgId;
                    }
                }

                if (md.MessageDispositon == SequenceMessageDisposition.ProcessingDisposition.HandleMessage)
                {
                    si.RemainingMessages.Remove(seqNum);
                    if (si.RemainingMessages.ContainsKey(seqNum + 1))
                    {
                        md.NextMessageId = si.RemainingMessages[seqNum + 1];
                    }
                }
                else if (md.MessageDispositon == SequenceMessageDisposition.ProcessingDisposition.Postpone)
                {
                    TimeSpan ts = si.LastMessageSeen - DateTime.Now;
                    ts = ts + ts + ts;
                    md.EstimatedRetry = DateTime.Now + ts;
                }
            });
            return md;
        }

        /// <summary>
        /// algorytm
        /// lastSeen - data pojawienia sie ostatniego komunikatu z tej serii
        /// seqNumber = 0 - handle, gdy seqLen = 0 lub 1 to nie zakladaj sekwencji
        /// seqNumber > 0
        /// jest rekord, cur number mniejszy o 1 - handle, update cur number
        /// jest rekord, cur number >= number - shit, nie powinno sie zdarzyc
        /// jest rekord, cur number mniejszy o 2 lub wiecej - 
        /// 
        /// </summary>
        /// <param name="seqId">sequence ID</param>
        /// <param name="seqNumber">number of message in this sequence</param>
        /// <param name="seqLen">sequence length, if known</param>
        /// <param name="transactionObj">current db transaction</param>
        /// <param name="messageId">Current message ID</param>
        /// <param name="estimatedPostponeDate">Estimated date when current message shoudl be retried</param>
        /// <param name="nextMessageId">Id of next message in this sequence</param>
        /// <returns></returns>
        public SequenceMessageDisposition SequenceMessageArrived(string seqId, int seqNumber, int? seqLen, object transactionObj, string messageId)
        {
            log.Debug("Seq message arrived. Seq = {0}:{1}, len: {2}", seqId, seqNumber, seqLen);
            if (!(transactionObj is DbConnection)) throw new ArgumentException("DB connection expected", "transactionObj");
            var conn = transactionObj as DbConnection;
            var ret = UpdateSequenceInfo(seqId, seqNumber, seqLen, messageId, conn);
            return ret;
        }

        public void Handle(InternalEvents.DatabaseInit message)
        {
            try
            {
                if (!AutoCreateTable) return;
                log.Info("Initializing the sequence table {0}", SequenceTable);
                SqlHelper.RunDDLFromResource(message.Connection, "NGinnBPM.MessageBus.create_seqtable.${dialect}.sql", new object[] { SequenceTable });
            }
            catch (DbException ex)
            {
                log.Warn("Failed to create sequence table {0}: {1}", SequenceTable, ex.Message);
            }
        }
    }
}
