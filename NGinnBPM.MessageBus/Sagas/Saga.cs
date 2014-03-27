using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NGinnBPM.MessageBus.Sagas
{

    public abstract class SagaBase 
    {
        public string Id { get; set; }
        public string StateVersion { get; set; }
        internal bool Completed { get; set; }

        /// <summary>
        /// Return this as saga id and the message will be ignored.
        /// </summary>
        public static readonly string IGNORE_MESSAGE = "nginn __ignore__";
        
        public IMessageBus MessageBus { get; set; }
        internal Dictionary<Type, Func<object, string>> MessageIds = null;
        protected virtual void Configure()
        {
        }

        /// <summary>
        /// True when the saga has been just created (has not been persisted yet)
        /// </summary>
        public bool IsNew { get; set; }

        /// <summary>
        /// Returns false if there was no saga id delegate for given message type
        /// </summary>
        /// <param name="message"></param>
        /// <param name="sagaId"></param>
        /// <returns></returns>
        internal bool TryGetSagaIdFromMessage(object message, out string sagaId)
        {
            sagaId = null;
            if (MessageIds == null)
            {
                MessageIds = new Dictionary<Type, Func<object, string>>();
                Configure();
            }
            Func<object, string> f;
            Type ct = message.GetType();
            while (ct.BaseType != null && ct.BaseType != typeof(Object))
            {
                if (MessageIds.TryGetValue(ct, out f))
                {
                    sagaId = f(message);
                    return true;
                }
            }
            return false;
        }
        

        internal abstract void InitializeSagaState(string id, string version, object state);
        internal abstract object GetState();
        
        public virtual void BeforeSave()
        {
        }
        
    }

    public class Saga<TSagaState> : SagaBase where TSagaState: new()
    {
        public TSagaState Data { get; set; }
        
        
        public Saga()
        {
            Id = null;
            Data = default(TSagaState);
        }

        internal override void InitializeSagaState(string id, string version, object state)
        {
            Id = id;
            StateVersion = version;
            Data = (TSagaState) state;
        }

        internal override object GetState()
        {
            return Data;
        }

        public virtual void InitializeState(string id, TSagaState state, string version)
        {
            Id = id;
            Data = state;
            StateVersion = version;
        }

        

        /// <summary>
        /// Define a function that will retrieve saga Id from the saga message
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="f"></param>
        protected void SagaId<T>(Func<T, string> f)
        {
            Func<object, string> r = x => f((T)x);
            MessageIds[typeof(T)] = r;
        }

        

        protected virtual void SetCompleted()
        {
            Completed = true;
        }
    }
}
