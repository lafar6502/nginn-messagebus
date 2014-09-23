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
	/// <summary>
	/// Queue operations for SQL server
	/// By default, we're using 'skip locked rows' hint to atomically select and lock next message for processing
	/// But if our database doesn't support such hint, we should rely on a different algorithm for skipping
	/// locked rows. One of such possibilities is to maintain a list of currently processed message IDs and skip
	/// these ids when selecting next row. We assume, however, that the database supports transactional select & lock for update operation.
	/// </summary>
	public class SqlQueueBase : ISqlQueue
	{
		private static readonly Logger log = LogManager.GetCurrentClassLogger();
		private static readonly Logger statLog = LogManager.GetCurrentClassLogger();
		
		public int MaxSqlParamsInBatch { get;set;}
		
		protected virtual string GetSqlFormatString(string fid)
		{
			switch(fid)
			{
				case "SelectAndLockNext1":
					return "select top 1 id, correlation_id, from_endpoint, to_endpoint, retry_count, msg_text, msg_headers, unique_id, retry_time from {0} with (UPDLOCK, READPAST) where subqueue='I' order by retry_time";
				case "SelectAndLockNext2":
					return "update {0} with(readpast, rowlock) set subqueue='X', last_processed = getdate() where id=@id and subqueue='I'";
				case "MarkMessageForProcessingLater":
					return "update {0} with(rowlock) set retry_time=@retry_time, last_processed=getdate(), subqueue='R' where id=@id";	
				case "InsertMessageBatch_InsertSql":
					return @"INSERT INTO {0} with(rowlock) ([from_endpoint], [to_endpoint],[subqueue],[insert_time],[last_processed],[retry_count],[retry_time],[error_info],[msg_text],[correlation_id],[label], [msg_headers], [unique_id])
                                    VALUES
                                    (@from_endpoint{1}, @to_endpoint{1}, @subqueue{1}, getdate(), null, 0, @retry_time{1}, null, {2}, @correl_id{1}, @label{1}, @headers{1}, @unique_id{1});";
				case "MoveMessageFromRetryToInput":
					return "update {0} with(readpast, rowlock) set subqueue='I' where id=@id and subqueue='R'";
				case "MarkMessageFailed":
					return "update {0} with(readpast, rowlock) set retry_count = retry_count + {1}, retry_time=@retry_time, error_info=@error_info, last_processed=getdate(), subqueue=@subq where id=@id";
				case "CleanupProcessedMessages":
					return  "delete top(10000) {0} with(READPAST) where retry_time <= @lmt and subqueue='X'";
				case "MoveScheduledMessagesToInputQueue":
					return "update top (1000) {0} with(READPAST) set subqueue='I' where subqueue='R' and retry_time <= getdate()";
				case "GetAverageLatencyMs":
					return "select coalesce(avg(DATEDIFF(millisecond, retry_time, last_processed)), 0) from {0} with(nolock) where retry_time >= @time_limit and subqueue='X'";
				case "RetryAllFailedMessages":
					return "update {0} with(READPAST) set subqueue='I', retry_count=0, error_info=null where subqueue='F'";
				case "GetSubqueueSize":
					return "select count(*) from {0} with(nolock) where subqueue='{1}'";
				case "MoveMessageToSubqueue":
					return "update {0} with(READPAST) set subqueue=@sq_to, error_info=null where id=@id'";
				default:
					throw new ArgumentException("fid invalid: " + fid);
			}
		}
		
		public virtual MessageContainer SelectAndLockNextInputMessage(DbConnection conn, string queueTable, Func<IEnumerable<string>> ignoreMe, out DateTime? retryTime, out bool moreMessages)
		{
			retryTime = null;
            var mc = new MessageContainer();
            moreMessages = false;        
            using (IDbCommand cmd = conn.CreateCommand())
            {
            	string sql = string.Format(GetSqlFormatString("SelectAndLockNext1"), queueTable);
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
                SqlUtil.AddParameter(cmd, "@id", Int64.Parse(mc.BusMessageId));
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
            var sql = string.Format(GetSqlFormatString("MarkMessageForProcessingLater"), queueTable);
            using (IDbCommand cmd = conn.CreateCommand())
            {
                cmd.CommandText = sql;
                SqlUtil.AddParameter(cmd, "@id", Convert.ToInt64(messageId));
                SqlUtil.AddParameter(cmd, "@retry_time", retryTime);
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
                        	System.Diagnostics.Debug.Assert(mw.BodyStr != null);
                        	if (mw.BodyStr == null) throw new Exception("Null message body string");
                            allMessages.Add(mw);
                            if (prevBody != mw.BodyStr)
                            {
                                prevBody = mw.BodyStr;
                                reuseBody = false;
                                bodyParam = "@msg_body" + cnt;
                            }
                            else reuseBody = true;
                            Dictionary<string, string> headers = RemoveNGHeaders(mw.Headers);


                            string sql = string.Format(GetSqlFormatString("InsertMessageBatch_InsertSql"), tableName, cnt, bodyParam);
                            cmd.CommandText += sql + "\n";

                            SqlUtil.AddParameter(cmd, "@from_endpoint" + cnt, mw.From);
                            SqlUtil.AddParameter(cmd, "@to_endpoint" + cnt, mw.To == null ? "" : mw.To);
                            SqlUtil.AddParameter(cmd, "@subqueue" + cnt, mw.IsScheduled ? "R" : "I");
                            SqlUtil.AddParameter(cmd, "@retry_time" + cnt, mw.IsScheduled ? mw.DeliverAt : (mw.HiPriority ? DateTime.Now.AddHours(-24) : DateTime.Now));
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
                            if (cmd.Parameters.Count >= MaxSqlParamsInBatch)
                            {
                            	cmd.ExecuteNonQuery();
                                cmd.CommandText = "";
                                cmd.Parameters.Clear();
                            }
                        }
                    }
                    if (cmd.CommandText.Length > 0)
                    {
                    	cmd.ExecuteNonQuery();
                    }
                }

                tm.Stop();
                log.Log(tm.ElapsedMilliseconds > (500 + allMessages.Count * 10) ? LogLevel.Warn : LogLevel.Trace, "Inserted batch of {0} messages ({1}). Time: {2}", allMessages.Count, "", tm.ElapsedMilliseconds);
                statLog.Info("InsertMessageBatchToQueue:{0}", tm.ElapsedMilliseconds);
                
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
		
		
		public void MarkMessageHandled(DbConnection conn, string queueTable, string messageId)
		{
			
		}
		
		public bool MoveMessageFromRetryToInput(DbConnection conn, string queueTable, string messageId)
		{
			DateTime t0 = DateTime.Now;
			string sql = GetSqlFormatString("MoveMessageFromRetryToInput");

            sql = string.Format(sql, queueTable);
            using (IDbCommand cmd = conn.CreateCommand())
            {
                cmd.CommandText = sql;
                SqlUtil.AddParameter(cmd, "@id", Convert.ToInt64(messageId));
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
            using (IDbCommand cmd = conn.CreateCommand())
            {
                cmd.CommandText = sql;
                SqlUtil.AddParameter(cmd, "@id", Convert.ToInt64(messageId));
                SqlUtil.AddParameter(cmd, "@retry_time", retryTime);
                SqlUtil.AddParameter(cmd, "@error_info", errorInfo);
                SqlUtil.AddParameter(cmd, "@subq", disp == MessageFailureDisposition.Fail ? "F" : "R");
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
        	using (IDbCommand cmd = conn.CreateCommand())
            {
        		cmd.CommandText = string.Format(GetSqlFormatString("CleanupProcessedMessages"), queueTable);
                SqlUtil.AddParameter(cmd, "@lmt", lmt);
                int n = cmd.ExecuteNonQuery();
                log.Info("Deleted {0} messages from {1}", n, queueTable);
            }
		}
		
		public virtual bool MoveScheduledMessagesToInputQueue(DbConnection conn, string queueTable)
        {

			string sql = string.Format(GetSqlFormatString("MoveScheduledMessagesToInputQueue"), queueTable);
            
            using (IDbCommand cmd = conn.CreateCommand())
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
            using (IDbCommand cmd = conn.CreateCommand())
            {
                cmd.CommandText = sql;
                SqlUtil.AddParameter(cmd, "@time_limit", DateTime.Now.AddMinutes(-5));
                return Convert.ToInt64(cmd.ExecuteScalar());
            }
        }
		
		public int GetInputQueueSize(DbConnection conn, string queueTable)
		{
			return GetSubqeueSize(conn, queueTable, "I");
		}
		
		public void RetryAllFailedMessages(DbConnection conn, string queueTable)
		{
			using (IDbCommand cmd = conn.CreateCommand())
            {
				cmd.CommandText = string.Format(GetSqlFormatString("RetryAllFailedMessages"), queueTable);
                int rows = cmd.ExecuteNonQuery();
                log.Info("{0} messages returned to queue {1}", rows, queueTable);
            }
		}
		
		
		
		
		public int GetSubqeueSize(DbConnection conn, string queueTable, string subqueue)
		{
			string sql = string.Format(GetSqlFormatString("GetSubqueueSize"), queueTable, subqueue);
            using (IDbCommand cmd = conn.CreateCommand())
            {
                cmd.CommandText = sql;
                return Convert.ToInt32(cmd.ExecuteScalar());
            }
		}

		public bool MoveMessageToSubqueue(DbConnection conn, string queueTable, string messageId, Subqueue toSubqueue, Subqueue? fromSubqueue)
		{
			using (IDbCommand cmd = conn.CreateCommand())
            {
				cmd.CommandText = string.Format(GetSqlFormatString("MoveMessageToSubqueue"), queueTable);
                if (fromSubqueue.HasValue) 
                {
                	SqlUtil.AddParameter(cmd, "sq_from", fromSubqueue.Value.ToString());
                	cmd.CommandText += " and  subqueue=@sq_from";
                }
                SqlUtil.AddParameter(cmd, "id", Convert.ToInt64(messageId));
                SqlUtil.AddParameter(cmd, "sq_to", toSubqueue.ToString());
                int rows = cmd.ExecuteNonQuery();
                log.Info("{0} messages returned to queue {1}", rows, queueTable);
                return rows > 0;
            }
		}
		
		
		public static ISqlQueue GetQueue(string dbProviderName)
		{
			string s = dbProviderName.ToLowerInvariant();
			if (s == "oracle" || s.Contains("oracle")) {
				return new OracleQueue();
			}
			return new SqlQueueBase();
		}
		
		public static ISqlQueue GetQueue(DbConnection conn)
		{
			return GetQueue(conn.GetType().Name);
		}
	}
}
