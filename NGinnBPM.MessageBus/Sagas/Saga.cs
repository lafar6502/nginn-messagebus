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
        
        
        public IMessageBus MessageBus { get; set; }
        internal Dictionary<Type, Func<object, string>> MessageIds = null;
        protected virtual void Configure()
        {
        }

        /// <summary>
        /// True when the saga has been just created (has not been persisted yet)
        /// </summary>
        public bool IsNew { get; set; }

        internal string GetSagaIdFromMessage(object message)
        {
            if (MessageIds == null)
            {
                MessageIds = new Dictionary<Type, Func<object, string>>();
                Configure();
            }
            if (MessageIds.ContainsKey(message.GetType())) return MessageIds[message.GetType()](message);
            return null;
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
