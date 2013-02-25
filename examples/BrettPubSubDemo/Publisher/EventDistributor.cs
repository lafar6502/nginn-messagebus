using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NGinnBPM.MessageBus;
using NLog;
namespace Publisher
{
    /// <summary>
    /// How to handle all incoming messages and re-distribute them with pub-sub
    /// Warning: the original message sender will be lost and replaced with event distributor's endpoint
    /// so you shouldn't reply to events distributed in this way (You usually don't reply in pub-sub, anyway)
    /// </summary>
    public class EventDistributor : IMessageConsumer<object>
    {
        public void Handle(object message)
        {
            if (message is NGinnBPM.MessageBus.Messages.ControlMessage) return; //don't distribute internal messages
            Console.WriteLine("EventDistributor now distributing {0}", message.GetType());
            MessageBusContext.CurrentMessageBus.Notify(message);
        }
    }
}
