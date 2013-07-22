using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;

namespace NGinnBPM.MessageBus.Impl
{
    /// <summary>
    /// Envelope for messages processed
    /// by NGinn MessageBus.
    /// </summary>
    [DataContract]
    public class MessageContainer : ICloneable
    {
        /// <summary>
        /// Sender endpoint
        /// </summary>
        [DataMember]
        public string From { get; set; }
        /// <summary>
        /// Destination endpoint
        /// </summary>
        [DataMember]
        public string To { get; set; }

        /// <summary>
        /// List of endpoints where message should be forwarded, apart from the main destination
        /// Used for sending the same message to multiple destinations. 
        /// This field is not serialized because the message should be cloned before sending
        /// TODO: implement this functionality in transport
        /// </summary>
        [IgnoreDataMember]
        public IList<string> Cc { get; set; }

        /// <summary>
        /// Message unique ID
        /// If you don't assign it, message bus will assign it when retrieving
        /// message from queue.
        /// </summary>
        [DataMember]
        public string UniqueId { get; set; }
        /// <summary>
        /// Message headers
        /// </summary>
        [DataMember]
        public Dictionary<string, string> Headers { get; set; }
        /// <summary>
        /// Message content - unserialized
        /// </summary>
        [IgnoreDataMember]
        public object Body { get; set; }
        /// <summary>
        /// High priority message.
        /// </summary>
        [IgnoreDataMember]
        public bool HiPriority { get; set; }
        /// <summary>
        /// Message content serialized into a string
        /// </summary>
        [DataMember]
        public string BodyStr { get; set; }

        public static readonly string HDR_RetryCount = "_NG_RetryCount";
        public static readonly string HDR_DeliverAt = "_NG-DeliverAt";
        public static readonly string HDR_Label = "_NG-Label";
        public static readonly string HDR_CorrelationId = "_NG-CorrelationId";
        /// <summary>
        /// Bus message id - internal message identifier, unique in single queue.
        /// </summary>
        public static readonly string HDR_BusMessageId = "_NG-BusMessageId";
        public static readonly string HDR_TTL = "_NG-TTL";
        public static readonly string HDR_ContentType = "_NG-ContentType";
        public static readonly string HDR_ReplyTo = "_NG-ReplyTo";
        /// <summary>
        /// Message sequence id number
        /// </summary>
        public static readonly string HDR_SeqId = "_NG-Seq";
        /// <summary>
        /// Message number in sequence (integer, starts with 0)
        /// </summary>
        public static readonly string HDR_SeqNum = "_NG-SeqNum";
        /// <summary>
        /// Sequence length (total number of messages in sequence)
        /// Optional but its better to specify it at some moment.
        /// </summary>
        public static readonly string HDR_SeqLen = "_NG-SeqLen";
        
        
        /// <summary>
        /// Message type header (message class name)
        /// </summary>
        public static readonly string HDR_MessageType = "_NG_MsgType";

        public bool IsValidHeader(string name, string value)
        {
            if (name.IndexOfAny(new char[] { '|', '=' }) >= 0) return false;
            if (value.IndexOfAny(new char[] { '|', '=' }) >= 0) return false;
            return true;
        }

        /// <summary>
        /// Headers as single string. For serialization
        /// purposes.
        /// </summary>
        [IgnoreDataMember]
        public string HeadersString
        {
            get
            {
                StringBuilder sb = new StringBuilder();
                foreach (string k in Headers.Keys)
                {
                    string v = Headers[k];
                    if (!IsValidHeader(k, v)) throw new Exception("Invalid header: " + k);
                    if (sb.Length > 0) sb.Append("|");
                    sb.Append(string.Format("{0}={1}", k, Headers[k]));
                }
                return sb.ToString();
            }

            set
            {
                Headers = null;
                if (value == null) return;
                if (value.Length == 0) return;
                Headers = new Dictionary<string, string>();
                string[] hdrs = value.Split('|');
                foreach (string pair in hdrs)
                {
                    if (pair.Length == 0) continue;
                    string[] nv = pair.Split('=');
                    if (nv.Length != 2) throw new Exception("Invalid header string: " + pair);
                    Headers[nv[0].Trim()] = nv[1].Trim();
                }
            }
        }

        public string GetStringHeader(string name, string defVal)
        {
            string str;
            if (Headers == null || !Headers.TryGetValue(name, out str))
                return defVal;
            if (str == null || str.Length == 0) return defVal;
            return str;
        }

        public int GetIntHeader(string name, int defVal)
        {
            string str;
            if (Headers == null || !Headers.TryGetValue(name, out str))
                return defVal;
            return Convert.ToInt32(str);
        }


        public DateTime GetDateTimeHeader(string name, DateTime defVal)
        {
            string str;
            if (Headers == null || !Headers.TryGetValue(name, out str))
                return defVal;
            return Convert.ToDateTime(str);
        }

        public void SetHeader(string name, string value)
        {
            if (Headers != null && Headers.ContainsKey(name)) Headers.Remove(name);
            if (value != null)
            {
                if (Headers == null) Headers = new Dictionary<string, string>();
                Headers[name] = value;
            }
        }

        public bool HasHeader(string hdr)
        {
            return Headers != null && Headers.ContainsKey(hdr);
        }

        /// <summary>
        /// Number of times current message has been retried
        /// </summary>
        [IgnoreDataMember]
        public int RetryCount
        {
            get
            {
                return GetIntHeader(HDR_RetryCount, 0);
            }
            set
            {
                SetHeader(HDR_RetryCount, value.ToString());
            }
        }

        /// <summary>
        /// Message type
        /// </summary>
        [IgnoreDataMember]
        public string MessageTypeName
        {
            get
            {
                return GetStringHeader(HDR_MessageType, null);
            }
            set
            {
                SetHeader(HDR_MessageType, value);
            }
        }

        /// <summary>
        /// Is message scheduled for delivery at specified time
        /// </summary>
        [IgnoreDataMember]
        public bool IsScheduled
        {
            get
            {
                return Headers != null && Headers.ContainsKey(HDR_DeliverAt);
            }
        }

        
        /// <summary>
        /// Planned message delivery date
        /// </summary>
        [IgnoreDataMember]
        public DateTime DeliverAt
        {
            get
            {
                return GetDateTimeHeader(HDR_DeliverAt, DateTime.Now);
            }
            set
            {
                SetHeader(HDR_DeliverAt, value.ToString("yyyy-MM-dd HH:mm:ss"));
            }
        }

        /// <summary>
        /// Correlation id
        /// </summary>
        [IgnoreDataMember]
        public string CorrelationId
        {
            get { return GetStringHeader(HDR_CorrelationId, null); }
            set { SetHeader(HDR_CorrelationId, value); }
        }

        /// <summary>
        /// Message ID assigned by bus. Unique only in a queue.
        /// </summary>
        [IgnoreDataMember]
        public string BusMessageId
        {
            get { return GetStringHeader(HDR_BusMessageId, null); }
            set { SetHeader(HDR_BusMessageId, value); }
        }

        [IgnoreDataMember]
        public string SequenceId
        {
            get
            {
                return GetStringHeader(HDR_SeqId, null);
            }
            set
            {
                SetHeader(HDR_SeqId, value);
            }
        }

        public int SequenceNumber
        {
            get { return GetIntHeader(HDR_SeqNum, -1); }
            set { SetHeader(HDR_SeqNum, value >= 0 ? value.ToString() : null); }
        }

        public int? SequenceLength
        {
            get { return HasHeader(HDR_SeqLen) ? (int?)GetIntHeader(HDR_SeqLen, 0) : null; }
            set { SetHeader(HDR_SeqLen, value > 0 ? value.ToString() : null); }
        }

        

        /// <summary>
        /// Message label header
        /// </summary>
        [IgnoreDataMember]
        public string Label
        {
            get { return GetStringHeader(HDR_Label, null); }
            set { SetHeader(HDR_Label, value); }
        }


        public override string ToString()
        {
            var l = Label;
            if (!string.IsNullOrEmpty(l)) return l;
            return Body != null ? Body.ToString() : "";
        }

        [IgnoreDataMember]
        public bool IsFinalRetry { get; set; }
        #region ICloneable Members

        public object Clone()
        {
            MessageContainer mc = new MessageContainer();
            mc.From = this.From;
            mc.To = this.To;
            if (Cc != null) 
                mc.Cc = new List<string>(this.Cc);
            if (this.Headers != null) mc.Headers = new Dictionary<string, string>(this.Headers);
            mc.Body = this.Body;
            mc.BodyStr = this.BodyStr;
            return mc;
        }

        #endregion
    }

    

    public delegate void MessageArrived(MessageContainer message, IMessageTransport source);

    

    /// <summary>
    /// Unused as for now
    /// </summary>
    public interface IMessageTransport
    {
        /// <summary>
        /// Local endpoint name (the sender endpoint)
        /// </summary>
        string Endpoint { get; }
        /// <summary>
        /// Send a message. Destination is in message.To field.
        /// </summary>
        /// <param name="message"></param>
        void Send(MessageContainer message);
        /// <summary>
        /// Send a batch of messages. Message destinations are in 'To' field.
        /// </summary>
        /// <param name="messages"></param>
        void SendBatch(IList<MessageContainer> messages, object conn);
        /// <summary>
        /// Process arriving message
        /// </summary>
        event MessageArrived OnMessageArrived;
        /// <summary>
        /// Raised when a message is inserted targeted at an unknown
        /// destination.
        /// </summary>
        event MessageArrived OnMessageToUnknownDestination;
        /// <summary>
        /// Current message information
        /// </summary>
        MessageContainer CurrentMessage { get; }
        
        /// <summary>
        /// Process current message again, later (later is transport specific)
        /// </summary>
        void ProcessCurrentMessageLater(DateTime howLater);
        
    }

    public interface IDbMessageTransport : IMessageTransport
    {
        /// <summary>
        /// Sends a batch of messages in specified DB connection
        /// Use if you have an open connection and want to use that instead 
        /// of creating a new one. 
        /// </summary>
        /// <param name="messages"></param>
        /// <param name="conn"></param>
        void SendBatch(IList<MessageContainer> messages, System.Data.IDbTransaction tran);
    }
}
