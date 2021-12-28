using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Messaging;
using NLog;

namespace NGinnBPM.MessageBus.Impl.MSMQ
{
    /// <summary>
    /// MSMQ message transport.
    /// </summary>
    class MSMQTransport : IMessageTransport, IStartableService
    {
        public IMessageContainerSerializer MessageSerializer { get; set; }
        private MessageQueue _mq;
        
        [ThreadStatic]
        private MessageContainer _curMsg;

        public string Endpoint
        {
            get { return "msmq:" + _mq.Path; }
        }

        public void Send(MessageContainer message)
        {
            if (string.IsNullOrEmpty(message.BodyStr))
                throw new Exception("Message BodyStr empty");
            throw new NotImplementedException();
            Message ms;
            ms.AcknowledgeType = AcknowledgeTypes.PositiveReceive;
            
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
            _mq = new MessageQueue(Endpoint);
            
        }

        public void Stop()
        {
            throw new NotImplementedException();
        }

        public bool IsRunning
        {
            get { throw new NotImplementedException(); }
        }


        public object CurrentTransaction
        {
            get { return null; }
        }
    }
}
