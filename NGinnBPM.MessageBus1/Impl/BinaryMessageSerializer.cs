using System;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Remoting.Messaging;
using System.IO;

namespace NGinnBPM.MessageBus.Impl
{
    /// <summary>
    /// Binary message serializer
    /// </summary>
    public class BinaryMessageSerializer : ISerializeMessages
    {
        private BinaryFormatter _bf;
        
        public BinaryMessageSerializer()
        {
            _bf = new BinaryFormatter(new RemotingSurrogateSelector(), new StreamingContext(StreamingContextStates.Persistence));
        }

        #region ISerializeMessages Members
        public void Serialize(object msg, System.IO.Stream stm)
        {
            _bf.Serialize(stm, msg);
        }

        public object Deserialize(System.IO.Stream stm)
        {
            return _bf.Deserialize(stm);
        }
        #endregion

        #region ISerializeMessages Members


        public void Serialize(object msg, TextWriter tw)
        {
            throw new NotImplementedException();
        }

        public object Deserialize(TextReader tr)
        {
            throw new NotImplementedException();
        }

        #endregion

        #region ISerializeMessages Members

        public bool TextSupported
        {
            get { return false; }
        }

        #endregion
    }
}
