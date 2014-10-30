/*
 * Created by SharpDevelop.
 * User: Rafal
 * Date: 2014-08-05
 * Time: 20:41
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
using System;
using System.Data;
using System.Collections.Generic;
using NLog;
using System.IO;
using System.Diagnostics;
using System.Text;
using System.Data.Common;


namespace NGinnBPM.MessageBus.Impl.SqlQueue
{
	public class CommonQueueOps : ISqlQueue
	{
		protected static readonly Logger log = LogManager.GetCurrentClassLogger();
		protected static readonly Logger statLog = LogManager.GetCurrentClassLogger();
		protected string _dialect;
		protected ISqlAbstractions _sql;
		
		public CommonQueueOps(string dialect)
		{
		    _dialect = dialect;
		    _sql = SqlHelper.GetSqlAbstraction(dialect);
		}
		
		protected string GetSqlFormatString(string queryId)
		{
		    return SqlHelper.GetNamedSqlQuery(queryId, _dialect);
		}
		
		public virtual MessageContainer SelectAndLockNextInputMessage(DbConnection conn, string queueTable, Func<IEnumerable<string>> currentIDs, out DateTime? retryTime, out bool moreMessages)
		{
			retryTime = null;;
            var mc = new MessageContainer();
            moreMessages = false;        
            using (DbCommand cmd = _sql.CreateCommand(conn))
            {
                string sql = string.Format(SqlHelper.GetNamedSqlQuery("SelectAndLockNext1", _dialect), queueTable);
                cmd.CommandText = sql;

                using (IDataReader dr = cmd.ExecuteReader())
                {
                    if (!dr.Read()) return null;
                    var rc = Convert.ToInt32(dr["retry_count"]);
                    mc.From = Convert.ToString(dr["from_endpoint"]);
                    mc.To = Convert.ToString(dr["to_endpoint"]);
                    mc.HeadersString = Convert.ToString(dr["msg_headers"]);
                    mc.SetHeader(MessageContainer.HDR_RetryCount, rc.ToString()); ;
                    mc.CorrelationId = Convert.ToString(dr["correlation_id"]);
                    mc.BusMessageId = Convert.ToString(dr["id"]);
                    mc.UniqueId = Convert.ToString(dr["unique_id"]);
                    retryTime = Convert.ToDateTime(dr["retry_time"]);
                    mc.BodyStr = dr.GetString(dr.GetOrdinal("msg_text"));
                }
                cmd.CommandText = string.Format(GetSqlFormatString("SelectAndLockNext2"), queueTable);
                _sql.AddParameter(cmd, "id", Int64.Parse(mc.BusMessageId));
                int cnt = cmd.ExecuteNonQuery();
                if (cnt == 0)
                {
                    log.Warn("Updated 0 rows when trying to lock message {0}. Skipping", mc.BusMessageId);
                    moreMessages = true;
                    return null;
                }
                else moreMessages = true;
                return mc;
            }
		}
		
		public virtual void MarkMessageForProcessingLater(DbConnection conn, string queueTable, string messageId, DateTime? retryTime)
		{
			DateTime t0 = DateTime.Now;
            var dialect = SqlHelper.GetDialect(conn.GetType());
			var sql = string.Format(SqlHelper.GetNamedSqlQuery("MarkMessageForProcessingLater", dialect), queueTable);
            using (DbCommand cmd = _sql.CreateCommand(conn))
            {
                cmd.CommandText = sql;
                _sql.AddParameter(cmd, "id", Convert.ToInt64(messageId));
                _sql.AddParameter(cmd, "retry_time", retryTime);
                int n = cmd.ExecuteNonQuery();
                if (n != 1) throw new Exception(string.Format("Failed to update message {0} when moving to retry queue", messageId));
            }
            TimeSpan ts = DateTime.Now - t0;
            log.Log(ts.TotalMilliseconds > 50.0 ? LogLevel.Warn : LogLevel.Trace, "MarkMessageForProcessingLater {0} update time: {1}", messageId, ts);
			
		}
		
		
		
		public virtual void InsertMessageBatchToLocalDatabaseQueues(DbConnection conn, IDictionary<string, ICollection<MessageContainer>> messages)
		{
            if (messages.Count == 0) return;
            var tm = Stopwatch.StartNew();
            var allMessages = new List<MessageContainer>();
            string qry = "";
            string qprm = "";
            try
            {
                using (DbCommand cmd = _sql.CreateCommand(conn))
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
                        	System.Diagnostics.Debug.Assert(mw.BodyStr != null);
                        	if (mw.BodyStr == null) throw new Exception("Null message body string");
                            allMessages.Add(mw);
                            if (prevBody != mw.BodyStr)
                            {
                                prevBody = mw.BodyStr;
                                reuseBody = false;
                                bodyParam = "msg_body" + cnt;
                            }
                            else reuseBody = true;
                            Dictionary<string, string> headers = RemoveNGHeaders(mw.Headers);


                            string sql = string.Format(GetSqlFormatString("InsertMessageBatch_InsertSql"), tableName, cnt, bodyParam);
                            cmd.CommandText += sql + "\n";

                            _sql.AddParameter(cmd, "from_endpoint" + cnt, mw.From);
                            _sql.AddParameter(cmd, "to_endpoint" + cnt, mw.To == null ? "" : mw.To);
                            _sql.AddParameter(cmd, "subqueue" + cnt, mw.IsScheduled ? "R" : "I");
                            _sql.AddParameter(cmd, "retry_time" + cnt, mw.IsScheduled ? mw.DeliverAt : (mw.HiPriority ? DateTime.Now.AddHours(-24) : DateTime.Now));
                            if (!reuseBody)
                            {
                                _sql.AddParameter(cmd, bodyParam, mw.BodyStr);
                            }
                            _sql.AddParameter(cmd, "correl_id" + cnt, mw.CorrelationId);
                            string s = mw.ToString();
                            if (string.IsNullOrEmpty(s)) s = mw.Body == null ? "" : mw.Body.ToString();
                            if (s.Length > 100) s = s.Substring(0, 100);

                            _sql.AddParameter(cmd, "label" + cnt, s);
                            _sql.AddParameter(cmd, "headers" + cnt, HeadersToString(headers));
                            _sql.AddParameter(cmd, "unique_id" + cnt, mw.UniqueId);
                            cnt++;
                            if (cmd.Parameters.Count >= _sql.MaxNumberOfQueryParams)
                            {
                                reuseBody = false;
                                qry = cmd.CommandText;
                                qprm = SqlAbstract_sqlserver.DumpCommandParams(cmd);
                                cmd.ExecuteNonQuery();
                                cmd.CommandText = "";
                                cmd.Parameters.Clear();
                            }
                        }
                    }
                    if (cmd.CommandText.Length > 0)
                    {
                        qry = cmd.CommandText;
                        qprm = SqlAbstract_sqlserver.DumpCommandParams(cmd);
                        cmd.ExecuteNonQuery();
                    }
                }

                tm.Stop();
                log.Log(tm.ElapsedMilliseconds > (500 + allMessages.Count * 10) ? LogLevel.Warn : LogLevel.Trace, "Inserted batch of {0} messages ({1}). Time: {2}", allMessages.Count, "", tm.ElapsedMilliseconds);
                statLog.Info("InsertMessageBatchToQueue:{0}", tm.ElapsedMilliseconds);
                
            }
            catch (Exception ex)
            {
                log.Error("Error inserting message batch: {0}\nQuery:\n{1}\n{2}", ex, qry, qprm);
                
                int cnt = 0;
                foreach (MessageContainer mc in allMessages)
                {
                    log.Error("Message: {0}", mc.ToString());
                    if (cnt++ > 5) break;
                }
                throw;
            }
		
		}
		
		
		public void MarkMessageHandled(DbConnection conn, string queueTable, string messageId)
		{
			
		}
		
		public bool MoveMessageFromRetryToInput(DbConnection conn, string queueTable, string messageId)
		{
			DateTime t0 = DateTime.Now;
			string sql = GetSqlFormatString("MoveMessageFromRetryToInput");

            sql = string.Format(sql, queueTable);
            using (DbCommand cmd = _sql.CreateCommand(conn))
            {
                cmd.CommandText = sql;
                _sql.AddParameter(cmd, "id", Convert.ToInt64(messageId));
                int n = cmd.ExecuteNonQuery();
                return n > 0;
            }
		}
		
		public bool MarkMessageFailed(DbConnection conn, string queueTable, string messageId, string errorInfo, MessageFailureDisposition disp, DateTime retryTime)
		{
			DateTime t0 = DateTime.Now;
            bool ret = true;
            string sql = GetSqlFormatString("MarkMessageFailed");
            sql = string.Format(sql, queueTable, disp == MessageFailureDisposition.RetryDontIncrementRetryCount ? 0 : 1);
            using (DbCommand cmd = _sql.CreateCommand(conn))
            {
                cmd.CommandText = sql;
                _sql.AddParameter(cmd, "retry_time", retryTime);
                _sql.AddParameter(cmd, "error_info", errorInfo);
                _sql.AddParameter(cmd, "subq", disp == MessageFailureDisposition.Fail ? "F" : "R");
                _sql.AddParameter(cmd, "id", Convert.ToInt64(messageId));
                
                int n = cmd.ExecuteNonQuery();
                if (n != 1)
                {
                    log.Warn("Failed to mark message {0} for retry. Probably someone else is handling it now...", messageId);
                    ret = false;
                }
            }
            TimeSpan ts = DateTime.Now - t0;
            log.Log(ts.TotalMilliseconds > 50.0 ? LogLevel.Warn : LogLevel.Trace, "MarkMessageFailed2 {0} update time: {1}", messageId, ts);
            return ret;
		}
		
		/// <summary>
        /// Remove nginn message headers that are kept in dedicated db columns
        /// </summary>
        /// <param name="msgHeaders"></param>
        /// <returns></returns>
        protected static Dictionary<string, string> RemoveNGHeaders(Dictionary<string, string> msgHeaders)
        {
            Dictionary<string, string> d = new Dictionary<string, string>();
            if (msgHeaders == null) return d;
            foreach (string k in msgHeaders.Keys)
            {
                if (k != MessageContainer.HDR_BusMessageId &&
                    k != MessageContainer.HDR_ContentType &&
                    k != MessageContainer.HDR_CorrelationId &&
                    k != MessageContainer.HDR_DeliverAt &&
                    k != MessageContainer.HDR_Label &&
                    k != MessageContainer.HDR_RetryCount)
                    d[k] = msgHeaders[k];
            }
            return d;
        }

        /// <summary>
        /// Convert message headers to a string
        /// </summary>
        /// <param name="headers"></param>
        /// <returns></returns>
        protected static string HeadersToString(Dictionary<string, string> headers)
        {
            StringBuilder sb = new StringBuilder();
            foreach (string k in headers.Keys)
            {
                string v = headers[k];
                if (sb.Length > 0) sb.Append("|");
                sb.Append(string.Format("{0}={1}", k, headers[k]));
            }
            return sb.ToString();
        }

        protected static Dictionary<string, string> StringToHeaders(string hdr)
        {
            Dictionary<string, string> Headers = new Dictionary<string, string>();
            if (hdr == null) return Headers;
            if (hdr.Length == 0) return Headers;
            string[] hdrs = hdr.Split('|');
            foreach (string pair in hdrs)
            {
                if (pair.Length == 0) continue;
                string[] nv = pair.Split('=');
                if (nv.Length != 2) throw new Exception("Invalid header string: " + pair);
                Headers[nv[0].Trim()] = nv[1].Trim();
            }
            return Headers;
        }

		public void CleanupProcessedMessages(DbConnection conn, string queueTable, DateTime? olderThan)
		{
		
			var lmt = olderThan.HasValue ? olderThan.Value : DateTime.Now.AddDays(-7);
        	using (DbCommand cmd = _sql.CreateCommand(conn))
            {
        		cmd.CommandText = string.Format(GetSqlFormatString("CleanupProcessedMessages"), queueTable);
                _sql.AddParameter(cmd, "lmt", lmt);
                log.Info("Exec query {0}", cmd.CommandText);
                int n = cmd.ExecuteNonQuery();
                log.Info("Deleted {0} messages from {1}", n, queueTable);
            }
		}
		
		public virtual bool MoveScheduledMessagesToInputQueue(DbConnection conn, string queueTable)
        {

			string sql = string.Format(GetSqlFormatString("MoveScheduledMessagesToInputQueue"), queueTable);
            
            using (DbCommand cmd = _sql.CreateCommand(conn))
            {
                cmd.CommandText = sql;
                int n = cmd.ExecuteNonQuery();
                if (n > 0)
                {
                    log.Info("Moved {0} messages from retry to input in queue {1}", n, queueTable);
                    return true;
                }
            }
            return false;
        }
		
		public long GetAverageLatencyMs(DbConnection conn, string queueTable)
        {
            
			string sql = string.Format(GetSqlFormatString("GetAverageLatencyMs"), queueTable);
            using (DbCommand cmd = _sql.CreateCommand(conn))
            {
                cmd.CommandText = sql;
                _sql.AddParameter(cmd, "time_limit", DateTime.Now.AddMinutes(-5));
                return Convert.ToInt64(cmd.ExecuteScalar());
            }
        }
		
		public int GetInputQueueSize(DbConnection conn, string queueTable)
		{
			return GetSubqeueSize(conn, queueTable, "I");
		}
		
		public void RetryAllFailedMessages(DbConnection conn, string queueTable)
		{
			using (DbCommand cmd = _sql.CreateCommand(conn))
            {
				cmd.CommandText = string.Format(GetSqlFormatString("RetryAllFailedMessages"), queueTable);
                int rows = cmd.ExecuteNonQuery();
                log.Info("{0} messages returned to queue {1}", rows, queueTable);
            }
		}
		
		
		
		
		public int GetSubqeueSize(DbConnection conn, string queueTable, string subqueue)
		{
			string sql = string.Format(GetSqlFormatString("GetSubqueueSize"), queueTable, subqueue);
            using (DbCommand cmd = _sql.CreateCommand(conn))
            {
                cmd.CommandText = sql;
                return Convert.ToInt32(cmd.ExecuteScalar());
            }
		}

		public bool MoveMessageToSubqueue(DbConnection conn, string queueTable, string messageId, Subqueue toSubqueue, Subqueue? fromSubqueue)
		{
			using (DbCommand cmd = _sql.CreateCommand(conn))
            {
				cmd.CommandText = string.Format(GetSqlFormatString("MoveMessageToSubqueue"), queueTable);
                if (fromSubqueue.HasValue) 
                {
                	_sql.AddParameter(cmd, "sq_from", fromSubqueue.Value.ToString());
                	cmd.CommandText += " and  subqueue=@sq_from";
                }
                _sql.AddParameter(cmd, "id", Convert.ToInt64(messageId));
                _sql.AddParameter(cmd, "sq_to", toSubqueue.ToString());
                int rows = cmd.ExecuteNonQuery();
                log.Info("{0} messages returned to queue {1}", rows, queueTable);
                return rows > 0;
            }
		}
	}
}
