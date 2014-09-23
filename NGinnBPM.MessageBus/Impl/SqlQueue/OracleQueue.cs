/*
 * Created by SharpDevelop.
 * User: Rafal
 * Date: 2014-08-11
 * Time: 22:26
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
using System;
using System.Data.Common;
using System.Collections.Generic;
using System.Linq;

namespace NGinnBPM.MessageBus.Impl.SqlQueue
{
	/// <summary>
	/// Description of OracleQueue.
	/// </summary>
	public class OracleQueue : SqlQueueBase
	{
		public OracleQueue()
		{
		}
		
		protected override string GetSqlFormatString(string fid)
		{
			switch(fid)
			{
				case "SelectAndLockNext1":
					return "select id, correlation_id, from_endpoint, to_endpoint, retry_count, msg_text, msg_headers, unique_id, retry_time from {0} " +
						"where subqueue='I' and rownum <= 1 order by retry_time FOR UPDATE SKIP LOCKED";
					
				case "SelectAndLockNext2":
					return "update {0} set subqueue='X' where subqueue='I' and id=:id";
					return "update {0} with(readpast, rowlock) set subqueue='X', last_processed = getdate() where id=@id and subqueue='I'";
				case "MarkMessageForProcessingLater":
					return "update {0} set retry_time=:retry_time, last_processed=CURRENT_DATE, subqueue='R' where id=:id";	
				case "InsertMessageBatch_InsertSql":
					return @"INSERT INTO {0} (from_endpoint, to_endpoint,subqueue,insert_time,last_processed,retry_count,retry_time,error_info,msg_text,correlation_id,label, msg_headers, unique_id)
                                    VALUES
                                    (:from_endpoint{1}, :to_endpoint{1}, :subqueue{1}, CURRENT_DATE, NULL, 0, :retry_time{1}, NULL, {2}, :correl_id{1}, :label{1}, :headers{1}, :unique_id{1});";
					
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
		
		public override void InsertMessageBatchToLocalDatabaseQueues(System.Data.Common.DbConnection conn, System.Collections.Generic.IDictionary<string, System.Collections.Generic.ICollection<MessageContainer>> messages)
		{
			using(var cmd = conn.CreateCommand())
			{
				foreach(string tb in messages.Keys)
				{
					foreach(var mc in messages[tb])
					{
						cmd.Parameters.Clear();
						var sql = this.GetSqlFormatString("InsertMessageBatch_InsertSql");
						sql = string.Format(sql, tb, "", "");
						//SqlUtil.AddParameter(cmd, ":dd", DbDataAdapter);
						
						cmd.CommandText = sql;
						cmd.ExecuteNonQuery();
						
					}
				}
			}
			throw new NotImplementedException();
		}
	}
}
