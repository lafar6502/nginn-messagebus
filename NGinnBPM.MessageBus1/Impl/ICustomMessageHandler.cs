using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NGinnBPM.MessageBus.Impl
{
    /// <summary>
    /// this one will get all messages from message bus
    /// and will decide on its own whether to handle the message or not.
    /// </summary>
    public interface ICustomMessageHandler
    {
        /// <summary>
        /// This method is called before dispatching a message to its handlers
        /// return true if the message should be dispatched
        /// or false if you want to cancel further processing of that message
        /// </summary>
        /// <param name="msg"></param>
        /// <returns></returns>
        bool PreHandle(object msg);
        /// <summary>
        /// This is invoked after all message handlers have been called
        /// </summary>
        /// <param name="msg"></param>
        void PostHandle(object msg);
    }
}
