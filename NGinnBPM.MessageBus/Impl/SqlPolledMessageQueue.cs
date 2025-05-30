using NGinnBPM.MessageBus.Impl.SqlQueue;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dapper;
using NGinnBPM.MessageBus.Messages;
using System.Collections.Concurrent;
using NLog;

namespace NGinnBPM.MessageBus.Impl
{
    public class SqlPolledMessageQueue
    {
        private static Logger log = LogManager.GetCurrentClassLogger();

        public string Endpoint { get; set; }

        public string ConnectionString { get; set; }

        public IMessageBus MessageBus { get; set; }

        /// <summary>
        /// default message time-to-live, in seconds.
        /// If message is still not processed after this time, it will fail with timeout
        /// It is possible to specify a different time to live via HDR_TTL header.
        /// 15 minutes by default
        /// </summary>
        public int DefaultMessageTTL { get; set; } = 15 * 60;

        /// <summary>
        /// time, in seconds, when a checked-out message should be acked as completed or failed.
        /// If not acked within that time, it will return to the input queue (or time-out)
        /// one minute (60s) by default
        /// </summary>
        public int DefaultAckTimeout { get; set; } = 60;

        private DbConnection OpenConnection()
        {
            return SqlHelper.OpenConnection(ConnectionString);
        }

        private void AccessDb(Action<DbConnection> act)
        {
            var sc = MessageBusContext.AppManagedConnection as DbConnection;
            if (sc != null && sc.State == System.Data.ConnectionState.Open)
            {
                act(sc);
                return;
            }
            using (sc = OpenConnection())
            {
                act(sc);
            }
        }

        private ConcurrentDictionary<string, AutoResetEvent> _waiters = new ConcurrentDictionary<string, AutoResetEvent>();

        private bool AwaitForWakeup(string clientName, int timeoutSeconds)
        {
            var ev = _waiters.GetOrAdd(clientName, (k) => new AutoResetEvent(false));
            var r = ev.WaitOne(timeoutSeconds * 1000);
            return r;
        }

        protected class MessageDTO
        {
            public int id { get; set; }
            public string unique_id { get; set; }

            public string from_endpoint { get; set; }
            public string to_endpoint { get; set; }
            public char subqueue { get; set; }
            public DateTime insert_time { get; set; }
            public DateTime retry_time { get; set; }
            public string correlation_id { get; set; }
            public string label { get; set; }
            public string msg_text { get; set; }
            public string msg_headers { get; set; }
        }

        IEnumerable<MessageContainer> CheckoutNextJobs(string clientId, int maxJobs, int ackTimeoutSeconds)
        {
            Newtonsoft.Json.Serialization.DefaultContractResolver sr;
            Newtonsoft.Json.JsonSerializer ser = null;
            var sw = new StringWriter();
            ser.Serialize(sw, DateTime.Now);
            var q = @"
            WITH    q AS
            (
                    select top {=limit} * 
		            from {=table} with(updlock, readpast)
		            where to_endpoint=@clientName and subqueue='I'
		            order by insert_time
            )
            UPDATE  q
            set Status='E', LockedBy=@myId, LockDeadline=DATEADD(second, @timeoutSecs, getdate())
            output inserted.*
            ";
            q = String.Format(q, clientId);
            throw new NotImplementedException();
        }



        public IEnumerable<MessageContainer> WaitAndCheckoutNextJobs(string clientName, int maxJobs, int ackTimeoutSeconds, int waitTimeoutSeconds = 30)
        {
            var d0 = DateTime.Now.AddSeconds(waitTimeoutSeconds);
            while (true)
            {
                var job = CheckoutNextJobs(clientName, maxJobs, ackTimeoutSeconds);
                if (job != null && job.Any()) return job;
                var timeRemain = (int)(d0 - DateTime.Now).TotalSeconds;
                if (timeRemain <= 0) break;
                if (!AwaitForWakeup(clientName, Math.Min(timeRemain, 30)))
                {
                    timeRemain = (int)(d0 - DateTime.Now).TotalSeconds;
                    if (timeRemain <= 0) break;
                }
                else
                {
                    log.Info("Client {0} notified", clientName);
                }
            }
            return Enumerable.Empty<MessageContainer>();
        }

        /// <summary>
        /// If message handling is successful, and result is returned
        /// the result message will be sent to the message bus, wrapped!!!
        /// if unsuccessful , we send failure message
        /// </summary>
        /// <param name="messageId"></param>
        /// <param name="success"></param>
        /// <param name="result"></param>
        void ConfirmMessageHandled(string messageId, bool success, object result)
        {
            throw new NotImplementedException();
            var q = @"update MQ_ with(readpast) set subqueue=@status, error_info=@message 
                output inserted.JobId, inserted.RefId, inserted.InsertTime, inserted.TypeName, inserted.QueueId
                where Id=@id and Status='E'";
            int n = 0;
            AccessDb(cn =>
            {
                int n = cn.Execute(q, new {message = success ? (string) null : result.ToString(), status = success ? 'X' : 'F'});
                if (n == 0) throw new Exception("Wrong message id or status");
            });
            if (success)
            {

            }
            else
            {
                
            }
        }


        /// <summary>
        /// time-out expired messages that still haven't been processed
        /// return un-acked messages to input queue if time has passed
        /// delete old messages.
        /// </summary>
        void CleanupTimeouts()
        {
            AccessDb(cn =>
            {
                var q2 = @"
                    update MQ_ with(rowlock, readpast)
                    set subqueue = 'I', retry_time = dateadd(second, insert_time, @ttl)
					output inserted.Id, inserted.from_endpoint, inserted.to_endpoint, inserted.insert_time, inserted.correlation_id, inserted.unique_id, inserted.headers
					where subqueue = 'E' and retry_time < getdate()
                    ";
                var ackTimeouts = cn.Query<MessageContainer>(q2, new { });
                
                foreach(var mc in ackTimeouts)
                {
                    if (!string.IsNullOrEmpty(mc.HeadersString)) //check ttl....
                    {
                        var defTimeout = DateTime.Now /* mc.insert time */ .AddSeconds(DefaultMessageTTL);
                        var timeout = mc.GetDateTimeHeader(MessageContainer.HDR_TTL, defTimeout);
                        cn.Execute("update MQ_ set retry_time=@timeout where id=@id", new { timeout = timeout, id = mc.BusMessageId });
                    }
                }

                var q3 = @"
                    update MQ_ with(rowlock, readpast)
                    set subqueue = 'F', error_info='timeout'
					output inserted.Id, inserted.from_endpoint, inserted.to_endpoint, inserted.insert_time, inserted.correlation_id, inserted.unique_id
					where subqueue = 'I' and retry_time < getdate()
                    ";
                var execTimeouts = cn.Query<MessageContainer>(q3, new { });
                foreach(var mc in execTimeouts)
                {
                    //publish timeout message...
                    var tm = new MessageHandlingTimeout
                    {
                        BusMessageId = mc.BusMessageId,
                        UniqueId = mc.UniqueId,
                        CorrelationId = mc.CorrelationId,
                        Queue = mc.To,
                        MessageType = "get the type!"
                    };
                    MessageBus.Send(mc.From, tm);
                }
                

            });
        }
    }
}
