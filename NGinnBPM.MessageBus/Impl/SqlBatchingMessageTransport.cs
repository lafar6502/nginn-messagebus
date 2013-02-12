using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NLog;
using System.Data.SqlTypes;
using System.Data.Common;
using System.Data;
using System.IO;

namespace NGinnBPM.MessageBus.Impl
{
    /// <summary>
    /// SQL message transport that batches messages sent to remote endpoints.
    /// For this purpose we group outgoing messages into batches
    /// Each batch contains messages sent to a single endpoint and its maximum size can be configured
    /// select top (BatchSize) * from queue with(updlock, readpast) where subqueue='J' order by batchid, retry_time
    /// update queue set subqueue='X' where id in (....)
    /// try
    /// {
    /// }
    /// catch(...)
    /// {
    /// }
    /// 
    /// </summary>
    public class SqlBatchingMessageTransport : SqlMessageTransport2
    {
        public SqlBatchingMessageTransport()
            : base()
        {
            BatchSize = 100;
        }

        public override void Start()
        {
            base.Start();
        }

        public override void Stop()
        {
            base.Stop();
        }

        private Dictionary<string, Batch> _batches = new Dictionary<string, Batch>();
        private int _batchCnt = 1;

        internal class Batch
        {
            public int Id { get; set; }
            public int Count { get; set; }
        }

        public int BatchSize { get; set; }

        protected virtual int GetBatchIdForNextMessageTo(string destination)
        {
            lock (_batches)
            {
                Batch b;
                if (!_batches.TryGetValue(destination, out b))
                {
                    b = new Batch
                    {
                        Id = _batchCnt++,
                        Count = 1
                    };
                    _batches[destination] = b;
                }
                else
                {
                    b.Count++;
                    if (b.Count > BatchSize)
                    {
                        b.Count = 0;
                        b.Id = _batchCnt++;
                    }
                }
                return b.Id;
            }
        }

        protected override string InsertMessageBatchToLocalDatabaseQueues(System.Data.IDbConnection conn, IDictionary<string, ICollection<MessageContainer>> messages)
        {
            string id = null;
            if (messages.Count == 0) return null;
            DateTime dt = DateTime.Now;
            var allMessages = new List<MessageContainer>();
            try
            {
                using (IDbCommand cmd = conn.CreateCommand())
                {
                    int cnt = 0;
                    cmd.CommandText = "";
                    string prevBody = null;
                    StringWriter sw = null;
                    bool reuseBody = false;
                    string bodyParam = null;
                    foreach (string tableName in messages.Keys)
                    {
                        ICollection<MessageContainer> lmc = messages[tableName];
                        foreach (MessageContainer mw in lmc)
                        {
                            bool isRemote = !string.Equals(mw.To, Endpoint, StringComparison.InvariantCultureIgnoreCase);
                            int batchId = GetBatchIdForNextMessageTo(mw.To);
      
                            allMessages.Add(mw);
                            System.Diagnostics.Debug.Assert(mw.BodyStr != null);
                            if (prevBody != mw.BodyStr)
                            {
                                prevBody = mw.BodyStr;
                                reuseBody = false;
                                bodyParam = "@msg_body" + cnt;
                            }
                            else reuseBody = true;
                            Dictionary<string, string> headers = RemoveNGHeaders(mw.Headers);


                            string sql = string.Format(@"INSERT INTO {0} with(rowlock) ([from_endpoint], [to_endpoint],[subqueue],[insert_time],[last_processed],[retry_count],[retry_time],[error_info],[msg_text],[correlation_id],[label], [msg_headers], [unique_id])
                                    VALUES
                                    (@from_endpoint{1}, @to_endpoint{1}, @subqueue{1}, getdate(), null, 0, @retry_time{1}, null, {2}, @correl_id{1}, @label{1}, @headers{1}, @unique_id{1});", tableName, cnt, bodyParam);
                            cmd.CommandText += sql + "\n";

                            SqlUtil.AddParameter(cmd, "@from_endpoint" + cnt, mw.From);
                            SqlUtil.AddParameter(cmd, "@to_endpoint" + cnt, mw.To == null ? "" : mw.To);
                            SqlUtil.AddParameter(cmd, "@subqueue" + cnt, isRemote ? "J" : (mw.IsScheduled ? "R" : "I"));
                            SqlUtil.AddParameter(cmd, "@retry_time" + cnt, mw.IsScheduled ? mw.DeliverAt : DateTime.Now);
                            SqlUtil.AddParameter(cmd, "@batch", (int?) batchId);
                            if (!reuseBody)
                            {
                                SqlUtil.AddParameter(cmd, bodyParam, mw.BodyStr);
                            }
                            SqlUtil.AddParameter(cmd, "@correl_id" + cnt, mw.CorrelationId);
                            string s = mw.ToString();
                            if (string.IsNullOrEmpty(s)) s = mw.Body == null ? "" : mw.Body.ToString();
                            if (s.Length > 100) s = s.Substring(0, 100);

                            SqlUtil.AddParameter(cmd, "@label" + cnt, s);
                            SqlUtil.AddParameter(cmd, "@headers" + cnt, HeadersToString(headers));
                            SqlUtil.AddParameter(cmd, "@unique_id" + cnt, mw.UniqueId);
                            cnt++;
                        }
                    }
                    cmd.CommandText += "select @@IDENTITY\n";
                    id = Convert.ToString(cmd.ExecuteScalar());
                }

                TimeSpan ts = DateTime.Now - dt;
                log.Log(ts.TotalMilliseconds > allMessages.Count * 5.0 ? LogLevel.Warn : LogLevel.Trace, "Inserted batch of {0} messages ({1}). Time: {2}", allMessages.Count, id, ts);
                statLog.Info("InsertMessageBatchToQueue|{0}|{1}|{2}", allMessages.Count, ts.TotalMilliseconds / allMessages.Count, ts.TotalMilliseconds);
                return id;
            }
            catch (Exception ex)
            {
                log.Error("Error inserting message batch: {0}", ex);
                int cnt = 0;
                foreach (MessageContainer mc in allMessages)
                {
                    log.Error("Message: {0}", mc.ToString());
                    if (cnt++ > 5) break;
                }
                throw;
            }
        }

        
        
    }
}
