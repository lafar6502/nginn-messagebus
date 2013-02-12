using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.ComponentModel;
using System.Data;
using NLog;

namespace NGinnBPM.MessageBus.Impl
{
    public class SequenceInfo
    {
        public string Id { get; set; }
        public int? LastProcessedNumber { get; set; }
        /// <summary>
        /// Map message Id-> message number in sequence
        /// </summary>
        public Dictionary<int, string> RemainingMessages { get; set; }
        public DateTime LastMessageSeen { get; set; }
        public int? Length { get; set; }
    }

    public class SqlSequenceManager : ISequenceMessages, ISupportInitialize
    {
        public SqlSequenceManager()
        {
            SequenceTable = "NGinnMessageBus_Sequences";
        }

        public string SequenceTable { get; set; }
        public bool AutoCreateTable { get; set; }
        public DbInitialize DbInit { get; set; }
        public int CacheSize { get; set; }

        /// <summary>Ids of currently processed sequences</summary>
        private HashSet<string> _currentlyProcessed = new HashSet<string>();
        private SimpleCache<string, SequenceInfo> _cache = new SimpleCache<string,SequenceInfo>();

        private object _lck = new object();
        private Logger log = LogManager.GetCurrentClassLogger();

        public void BeginInit()
        {
            
        }

        public void EndInit()
        {
            if (AutoCreateTable)
            {
                log.Info("Initializing the sequence table {0}", SequenceTable);
                DbInit.RunDbScript("NGinnBPM.MessageBus.create_seqtable.mssql.sql", new object[] { SequenceTable });
            }
        }

        /// <summary>
        /// get the sequence info for specified seq
        /// updates the sequence table only if the sequence message will be processed now
        /// </summary>
        /// <param name="seqId"></param>
        /// <param name="tran"></param>
        /// <param name="act"></param>
        private SequenceMessageDisposition UpdateSequenceInfo(string seqId, int seqNum, int? seqLen, string msgId, IDbTransaction tran)
        {
            var inserted = false;
            var md = new SequenceMessageDisposition();

            var si =_cache.Get(seqId, delegate(string seq) 
            {
                using (IDbCommand cmd = tran.Connection.CreateCommand())
                {
                    cmd.Transaction = tran;
                    cmd.CommandText = string.Format("select cur_num, seq_len, last_seen_date from {0} with(updlock) where seq_id='{1}'", SequenceTable, seqId);

                    SequenceInfo ret = new SequenceInfo { Id = seqId, LastMessageSeen = DateTime.Now, Length = seqLen };

                    using (IDataReader dr = cmd.ExecuteReader())
                    {
                        if (dr.Read())
                        {
                            ret.LastProcessedNumber = dr.IsDBNull(0) ? (int?)null : dr.GetInt32(0);
                            ret.Length = dr.IsDBNull(1) ? ret.Length : dr.GetInt32(1);
                            ret.LastMessageSeen = dr.GetDateTime(2);
                            return ret;
                        }
                    }
                    log.Debug("Inserting sequence {0}", seqId);
                    cmd.CommandText = string.Format("insert into {0} ([seq_id], [cur_num], [last_seen_date], [seq_len]) values (@seq_id, @cur_num, getdate(), @seq_len)", SequenceTable);
                    SqlUtil.AddParameter(cmd, "@cur_num", seqNum == 0 ? 0 : (int?)null);
                    SqlUtil.AddParameter(cmd, "@seq_id", seqId);
                    SqlUtil.AddParameter(cmd, "@seq_len", seqLen.HasValue ? seqLen.Value : (int?)null);
                    cmd.ExecuteNonQuery();
                    ret.LastProcessedNumber = seqNum;
                    if (seqNum > 0) ret.RemainingMessages.Add(seqNum, msgId);
                    inserted = true;
                    return ret;
                }
            });

            if (inserted)
            {
                if (seqNum == 0)
                {
                    md.MessageDispositon = SequenceMessageDisposition.ProcessingDisposition.HandleMessage;
                }
                else
                {
                    md.MessageDispositon = SequenceMessageDisposition.ProcessingDisposition.Postpone;
                    md.EstimatedRetry = DateTime.Now.AddMinutes(1);
                }
                return md;
            }

            if (si.LastProcessedNumber.HasValue && si.LastProcessedNumber.Value >= seqNum)
                throw new Exception(string.Format("Duplicated sequence message {0} (msgId={1}). Sequence {2} is currently at position {3}", seqNum, msgId, seqId, si.LastProcessedNumber));
            if ((!si.LastProcessedNumber.HasValue && seqNum == 0) ||
                (si.LastProcessedNumber.HasValue && seqNum == si.LastProcessedNumber + 1))
            {
                //update the sequence and process message
                using (IDbCommand cmd = tran.Connection.CreateCommand())
                {
                    si.LastProcessedNumber = seqNum;
                    si.LastMessageSeen = DateTime.Now;
                    si.RemainingMessages.Remove(seqNum);

                    cmd.CommandText = string.Format("update {0} set cur_num=@cur_num, last_seen_date=getdate(), seq_len=@seq_len where seq_id=@seq_id", SequenceTable);
                    log.Debug("Advancing sequence {0}:{1}", seqId, seqNum);
                    SqlUtil.AddParameter(cmd, "@cur_num", (int?) seqNum);
                    SqlUtil.AddParameter(cmd, "@seq_id", seqId);
                    SqlUtil.AddParameter(cmd, "@seq_len", si.Length.HasValue ? si.Length.Value : (int?)null);
                    cmd.ExecuteNonQuery();
                }
                md.MessageDispositon = SequenceMessageDisposition.ProcessingDisposition.HandleMessage;
                string t;
                if (si.RemainingMessages.TryGetValue(seqNum + 1, out t)) md.NextMessageId = t;
                return md;
            }
            else
            {
                //postpone the message and remenber its id
                si.RemainingMessages.Add(seqNum, msgId);
                md.MessageDispositon = SequenceMessageDisposition.ProcessingDisposition.Postpone;
                md.EstimatedRetry = DateTime.Now.Add(DateTime.Now - si.LastMessageSeen).AddSeconds(10);
                si.LastMessageSeen = DateTime.Now;
                return md;
            }
        }


        /// <summary>
        /// get the sequence info for specified seq
        /// updates the sequence table only if the sequence message will be processed now
        /// </summary>
        /// <param name="seqId"></param>
        /// <param name="tran"></param>
        /// <param name="act"></param>
        private SequenceMessageDisposition UpdateSequenceInfo(string seqId, int seqNum, int? seqLen, string msgId, IDbConnection con)
        {
            var inserted = false;
            var md = new SequenceMessageDisposition();

            var si = _cache.Get(seqId, delegate(string seq)
            {
                using (IDbCommand cmd = con.CreateCommand())
                {
                    cmd.CommandText = string.Format("select cur_num, seq_len, last_seen_date from {0} with(updlock) where seq_id='{1}'", SequenceTable, seqId);

                    SequenceInfo ret = new SequenceInfo { Id = seqId, LastMessageSeen = DateTime.Now, Length = seqLen };

                    using (IDataReader dr = cmd.ExecuteReader())
                    {
                        if (dr.Read())
                        {
                            ret.LastProcessedNumber = dr.IsDBNull(0) ? (int?)null : dr.GetInt32(0);
                            ret.Length = dr.IsDBNull(1) ? ret.Length : dr.GetInt32(1);
                            ret.LastMessageSeen = dr.GetDateTime(2);
                            return ret;
                        }
                    }
                    log.Debug("Inserting sequence {0}", seqId);
                    cmd.CommandText = string.Format("insert into {0} ([seq_id], [cur_num], [last_seen_date], [seq_len]) values (@seq_id, @cur_num, getdate(), @seq_len)", SequenceTable);
                    SqlUtil.AddParameter(cmd, "@cur_num", seqNum == 0 ? 0 : (int?)null);
                    SqlUtil.AddParameter(cmd, "@seq_id", seqId);
                    SqlUtil.AddParameter(cmd, "@seq_len", seqLen.HasValue ? seqLen.Value : (int?)null);
                    cmd.ExecuteNonQuery();
                    ret.LastProcessedNumber = seqNum;
                    if (seqNum > 0) ret.RemainingMessages.Add(seqNum, msgId);
                    inserted = true;
                    return ret;
                }
            });

            if (inserted)
            {
                if (seqNum == 0)
                {
                    md.MessageDispositon = SequenceMessageDisposition.ProcessingDisposition.HandleMessage;
                }
                else
                {
                    md.MessageDispositon = SequenceMessageDisposition.ProcessingDisposition.Postpone;
                    md.EstimatedRetry = DateTime.Now.AddMinutes(1);
                }
                return md;
            }

            if (si.LastProcessedNumber.HasValue && si.LastProcessedNumber.Value >= seqNum)
                throw new Exception(string.Format("Duplicated sequence message {0} (msgId={1}). Sequence {2} is currently at position {3}", seqNum, msgId, seqId, si.LastProcessedNumber));
            if ((!si.LastProcessedNumber.HasValue && seqNum == 0) ||
                (si.LastProcessedNumber.HasValue && seqNum == si.LastProcessedNumber + 1))
            {
                //update the sequence and process message
                using (IDbCommand cmd = con.CreateCommand())
                {
                    si.LastProcessedNumber = seqNum;
                    si.LastMessageSeen = DateTime.Now;
                    si.RemainingMessages.Remove(seqNum);

                    cmd.CommandText = string.Format("update {0} set cur_num=@cur_num, last_seen_date=getdate(), seq_len=@seq_len where seq_id=@seq_id", SequenceTable);
                    log.Debug("Advancing sequence {0}:{1}", seqId, seqNum);
                    SqlUtil.AddParameter(cmd, "@cur_num", (int?)seqNum);
                    SqlUtil.AddParameter(cmd, "@seq_id", seqId);
                    SqlUtil.AddParameter(cmd, "@seq_len", si.Length.HasValue ? si.Length.Value : (int?)null);
                    cmd.ExecuteNonQuery();
                }
                md.MessageDispositon = SequenceMessageDisposition.ProcessingDisposition.HandleMessage;
                string t;
                if (si.RemainingMessages.TryGetValue(seqNum + 1, out t)) md.NextMessageId = t;
                return md;
            }
            else
            {
                //postpone the message and remenber its id
                si.RemainingMessages.Add(seqNum, msgId);
                md.MessageDispositon = SequenceMessageDisposition.ProcessingDisposition.Postpone;
                md.EstimatedRetry = DateTime.Now.Add(DateTime.Now - si.LastMessageSeen).AddSeconds(10);
                si.LastMessageSeen = DateTime.Now;
                return md;
            }
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
            var ret = new SequenceMessageDisposition { MessageDispositon = SequenceMessageDisposition.ProcessingDisposition.RetryImmediately };
            SequenceInfo sequence = null;
            lock (_lck)
            {
                if (_currentlyProcessed.Contains(seqId))
                    return new SequenceMessageDisposition { MessageDispositon = SequenceMessageDisposition.ProcessingDisposition.RetryImmediately };
                _currentlyProcessed.Add(seqId);
            }
            try
            {
                if (transactionObj is IDbTransaction)
                {
                    ret = UpdateSequenceInfo(seqId, seqNumber, seqLen, messageId, transactionObj as IDbTransaction);
                }
                else
                {
                    ret = UpdateSequenceInfo(seqId, seqNumber, seqLen, messageId, (IDbConnection) transactionObj);
                }
                return ret;
            }
            finally
            {
                lock (_lck)
                {
                    bool b = _currentlyProcessed.Remove(seqId);
                }
                log.Debug("Finished processing seq {0}:{1}", seqId, seqNumber);
            }
        }
    }
}
