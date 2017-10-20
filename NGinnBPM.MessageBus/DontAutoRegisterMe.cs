using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NGinnBPM.MessageBus
{
    /// <summary>
    /// marker interface telling the message bus configurator not to 
    /// automatically register this message handler class.
    /// </summary>
    public interface DontAutoRegisterMe
    {
    }
}
