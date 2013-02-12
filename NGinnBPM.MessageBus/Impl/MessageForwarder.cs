using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NGinnBPM.MessageBus.Impl
{
    /// <summary>
    /// Forwards messages to speicfied endpoint
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class MessageForwarder<T> : IMessageConsumer<T>
    {
        private IMessageBus _mbus;

        public MessageForwarder(IMessageBus msgbus)
        {
            _mbus = msgbus;
        }

        public string TargetEndpoint { get; set; }

        #region IMessageConsumer<T> Members

        public void Handle(T message)
        {
            if (TargetEndpoint == null || TargetEndpoint.Length == 0)
                throw new Exception("No target endpoint");
            _mbus.Send(TargetEndpoint, message);
        }

        #endregion
    }
}
