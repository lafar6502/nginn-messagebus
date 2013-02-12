using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NGinnBPM.MessageBus.Messages
{
    [Serializable]
    public class ControlMessage
    {
    }

    [Serializable]
    public class PauseMessageProcessing : ControlMessage
    {
    }

    [Serializable]
    public class ResumeMessageProcessing : ControlMessage
    {
    }

    /// <summary>
    /// Inform message bus that message handler configuration
    /// has changed. 
    /// </summary>
    [Serializable]
    public class MessageHandlersModified : ControlMessage
    {
    }
}
