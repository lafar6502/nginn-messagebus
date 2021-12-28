using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace NGinnBPM.MessageBus.Impl
{
    /// <summary>
    /// For serializing messages together with all accompanying information
    /// </summary>
    public interface IMessageContainerSerializer
    {
        MessageContainer Deserialize(string s, bool deserializeBody);
        string Serialize(MessageContainer mc);
    }
}
