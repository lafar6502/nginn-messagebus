using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;
using NLog;
using Newtonsoft.Json;

namespace NGinnBPM.MessageBus.Impl
{
    /// <summary>
    /// Unused as for now
    /// </summary>
    public class HttpMessageTransport : IMessageTransport
    {
        private Logger log = LogManager.GetCurrentClassLogger();
        private JsonSerializer _ser;
        
        public string Endpoint { get; set; }

        public HttpMessageTransport()
        {
            JsonSerializerSettings s = new JsonSerializerSettings();
            s.Converters.Add(new Newtonsoft.Json.Converters.IsoDateTimeConverter());
            _ser = new JsonSerializer();
        }

        #region IMessageTransport Members

        public void Send(MessageContainer message)
        {
            try
            {
                WebClient wc = new WebClient();
                StringWriter sw = new StringWriter();
                message.From = this.Endpoint;
                _ser.Serialize(sw, message);
                string ret = wc.UploadString(message.To, sw.ToString());
            }
            catch (Exception ex)
            {
                log.Error("Error sending message {0}: {1}", message, ex);
                throw;
            }
        }

        #endregion

        #region IMessageTransport Members


        public void SendBatch(IList<MessageContainer> messages, object conn)
        {
            try
            {
                string to = messages[0].To;
                List<MessageContainer> lst = new List<MessageContainer>();
                foreach (MessageContainer mc in messages)
                {
                    mc.From = this.Endpoint;
                    lst.Add(mc);
                }
                WebClient wc = new WebClient();
                StringWriter sw = new StringWriter();
                _ser.Serialize(sw, lst);
                string ret = wc.UploadString(to, sw.ToString());
            }
            catch (Exception ex)
            {
                log.Error("Error sending messages {0}", ex);
                throw;
            }
        }

        public event MessageArrived OnMessageArrived;

        #endregion

        internal void HandleIncomingMessage(TextReader input)
        {
            MessageContainer mc = null;
            try
            {
                JsonReader jsr = new JsonTextReader(input);
                mc = _ser.Deserialize<MessageContainer>(jsr);
                if (mc.To == null || mc.To.Length == 0) mc.To = Endpoint;
                if (OnMessageArrived != null)
                    OnMessageArrived(mc, this);
            }
            catch (Exception ex)
            {
                log.Error("Error handling incoming message: {0}", ex);
                throw;
            }
        }


        #region IMessageTransport Members


        public MessageContainer CurrentMessage
        {
            get { throw new NotImplementedException(); }
        }

        public void ProcessCurrentMessageLater(DateTime howLater)
        {
            throw new NotImplementedException();
        }

        #endregion

        #region IMessageTransport Members


        public event MessageArrived OnMessageToUnknownDestination;

        #endregion


        public object CurrentTransaction
        {
            get { return null; }
        }
    }
}
