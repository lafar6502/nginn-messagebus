using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Newtonsoft.Json;

namespace NGinnBPM.MessageBus.Impl
{
    public class FileQueueTransport : IMessageTransport
    {
        public string Endpoint { get; set; }
        public TimeSpan RetryFolderCheckFrequency { get; set; }

        public void Send(MessageContainer message)
        {
            var fn = Guid.NewGuid().ToString("N") + ".json";
            using (var sw = new StreamWriter(fn, false, Encoding.UTF8))
            {
                sw.Write(JsonConvert.SerializeObject(message));
            }
        }

        public void SendBatch(IList<MessageContainer> messages, object conn)
        {
            foreach (var m in messages)
                Send(m);
        }

        public event MessageArrived OnMessageArrived;

        public event MessageArrived OnMessageToUnknownDestination;

        public MessageContainer CurrentMessage
        {
            get { throw new NotImplementedException(); }
        }

        public void ProcessCurrentMessageLater(DateTime howLater)
        {
            throw new NotImplementedException();
        }


        public object CurrentTransaction
        {
            get { return null; }
        }
    }
}
