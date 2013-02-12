using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NLog;

namespace NGinnBPM.MessageBus.Impl
{
    /// <summary>
    /// Bridge between Http transport and SQL transport.
    /// </summary>
    public class HttpMessageGateway
    {
        private IMessageTransport _bus;
        private IMessageTransport _http;
        private Logger log = LogManager.GetCurrentClassLogger();
        
        public IReceivedMessageRegistry ReceivedMessageRegistry { get; set; }



        public HttpMessageGateway(IMessageTransport busTransport, IMessageTransport httpTransport)
        {
            _bus = busTransport;
            _http = httpTransport;
            httpTransport.OnMessageArrived += new MessageArrived(httpTransport_OnMessageArrived);
        }

        void httpTransport_OnMessageArrived(MessageContainer message, IMessageTransport transport)
        {
            if (message.RetryCount > 0)
            { //we don't check if already received if message is sent for the first time
                if (ReceivedMessageRegistry.HasBeenReceived(message.UniqueId))
                {
                    log.Warn("Message {0} has already been received. Ignoring", message.UniqueId);
                    return;
                }
            }
            log.Info("Forwarding incoming http message {0} (from {1}) to {2}", message, message.From, _bus.Endpoint);
            message.To = _bus.Endpoint;
            _bus.Send(message);
            ReceivedMessageRegistry.RegisterReceived(message.UniqueId);
        }
    }
}
