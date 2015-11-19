using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NGinnBPM.MessageBus.Impl;

namespace NGinnBPM.MessageBus.Azure
{
    class AzureMessageTransport : IMessageTransport, IStartableService
    {
        public string Endpoint
        {
            get { throw new NotImplementedException(); }
        }

        public void Send(MessageContainer message)
        {
            throw new NotImplementedException();
        }

        public void SendBatch(IList<MessageContainer> messages, object conn)
        {
            throw new NotImplementedException();
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

        public void Start()
        {
            throw new NotImplementedException();
        }

        public void Stop()
        {
            throw new NotImplementedException();
        }

        public bool IsRunning
        {
            get { throw new NotImplementedException(); }
        }
    }
}
