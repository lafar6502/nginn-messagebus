
using System;
using System.Data.Common;
using System.Collections.Generic;
using System.Linq;
using System.Data.SqlTypes;
using System.Diagnostics;

namespace NGinnBPM.MessageBus.Impl.SqlQueue
{
    /// <summary>
    /// Description of OracleQueueOps.
    /// </summary>
    public class OracleQueueOps : CommonQueueOps
    {
        public OracleQueueOps() : base("oracle")
        {
            
        }
        
        public override void InsertMessageBatchToLocalDatabaseQueues(DbConnection conn, IDictionary<string, ICollection<MessageContainer>> messages)
        {
            if (messages.Count == 0) return;
            var tm = Stopwatch.StartNew();
            var allMessages = new List<MessageContainer>();
        
            using (DbCommand cmd = conn.CreateCommand())
            {
                int cnt = 0;
                foreach (string tableName in messages.Keys)
                {
                    ICollection<MessageContainer> lmc = messages[tableName];
                    foreach (MessageContainer mw in lmc)
                    {
                    	System.Diagnostics.Debug.Assert(mw.BodyStr != null);
                    	if (mw.BodyStr == null) throw new Exception("Null message body string");
                        Dictionary<string, string> headers = RemoveNGHeaders(mw.Headers);
                        string sql = string.Format(GetSqlFormatString("InsertMessageBatch_InsertSql"), tableName, "");
                        cmd.CommandText = sql;
                        log.Info("Executing {0}", cmd.CommandText);

                        _sql.AddParameter(cmd, "from_endpoint", mw.From);
                        _sql.AddParameter(cmd, "to_endpoint", mw.To == null ? "" : mw.To);
                        _sql.AddParameter(cmd, "subqueue", mw.IsScheduled ? "R" : "I");
                        _sql.AddParameter(cmd, "retry_time", mw.IsScheduled ? mw.DeliverAt : (mw.HiPriority ? DateTime.Now.AddHours(-24) : DateTime.Now));
                        _sql.AddParameter(cmd, "msgtext", mw.BodyStr);
                        _sql.AddParameter(cmd, "correl_id", mw.CorrelationId);
                        string s = mw.ToString();
                        if (string.IsNullOrEmpty(s)) s = mw.Body == null ? "" : mw.Body.ToString();
                        if (s.Length > 100) s = s.Substring(0, 100);
                        _sql.AddParameter(cmd, "label" + cnt, s);
                        _sql.AddParameter(cmd, "headers" + cnt, HeadersToString(headers));
                        _sql.AddParameter(cmd, "unique_id" + cnt, mw.UniqueId);
                        cmd.ExecuteNonQuery();
                        cmd.Parameters.Clear();
                    }
                }
            }

            //tm.Stop();
            //log.Log(tm.ElapsedMilliseconds > (500 + allMessages.Count * 10) ? LogLevel.Warn : LogLevel.Trace, "Inserted batch of {0} messages ({1}). Time: {2}", allMessages.Count, "", tm.ElapsedMilliseconds);
            //statLog.Info("InsertMessageBatchToQueue:{0}", tm.ElapsedMilliseconds);
        }
    }
}
