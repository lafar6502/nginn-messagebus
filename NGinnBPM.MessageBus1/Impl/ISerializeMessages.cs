using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace NGinnBPM.MessageBus.Impl
{
    /// <summary>
    /// Interface for message serializer.
    /// Implementer
    /// </summary>
    public interface ISerializeMessages
    {
        /// <summary>
        /// Serialize message in text format.
        /// </summary>
        /// <param name="msg"></param>
        /// <param name="tw"></param>
        void Serialize(object msg, TextWriter tw);
        /// <summary>
        /// Deserialize message in text format
        /// </summary>
        /// <param name="tr"></param>
        /// <returns></returns>
        object Deserialize(TextReader tr);
    }

    

}
