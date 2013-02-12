using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using NGinnBPM.MessageBus.Sagas;
using NLog;


namespace NGinnBPM.MessageBus.Impl
{
    /// <summary>
    /// Message dispatcher delivers messages to handlers
    /// PROBLEM: it caches message handler information so any
    /// modifications of handlers in runtime will not be noticed.
    /// </summary>
    public class MessageDispatcher
    {

        public bool RequireHandler { get; set; }
        public IServiceResolver ServiceLocator { get; set; }
        private Logger log = LogManager.GetCurrentClassLogger();
        /// <summary>
        /// for delivering messages to sagas...
        /// </summary>
        public Sagas.SagaStateHelper SagaHandler { get; set; }

        public class MsgHandlerInfo
        {
            public Type MessageHandlerGenericType;
            public Type InitiatedByGenericType;
            public Type MessageType;
            public MessageHandlerMethodDelegate HandleMethod;
        }

        private Dictionary<Type, MsgHandlerInfo> _handlers = new Dictionary<Type, MsgHandlerInfo>();

        protected MsgHandlerInfo GetHandlersFor(Type msgType)
        {
            MsgHandlerInfo mi;
            if (_handlers.TryGetValue(msgType, out mi))
                return mi;
            lock (this)
            {
                if (_handlers.TryGetValue(msgType, out mi))
                    return mi;
                mi = new MsgHandlerInfo();
                mi.MessageType = msgType;
                mi.MessageHandlerGenericType = typeof(IMessageConsumer<>).MakeGenericType(msgType);
                mi.InitiatedByGenericType = typeof(InitiatedBy<>).MakeGenericType(msgType); 
                mi.HandleMethod = DelegateFactory.CreateMessageHandlerDelegate(mi.MessageHandlerGenericType.GetMethod("Handle"));
                _handlers[msgType] = mi;
                return mi;
            }
        }

        /// <summary>
        /// Call this method when message handlers have been modified
        /// </summary>
        public void HandlerConfigurationChanged()
        {
            this._handlers = new Dictionary<Type, MsgHandlerInfo>();
        }


        protected virtual bool CallHandler(object message, object handler, MsgHandlerInfo mhi, IMessageBus bus)
        {
            if (handler is SagaBase)
            {
                string id = null;
                if (bus != null && bus.CurrentMessageInfo != null)
                {
                    id = bus.CurrentMessageInfo.CorrelationId;
                }
                bool isInitiator = mhi.InitiatedByGenericType.IsAssignableFrom(handler.GetType());
                var b = SagaHandler.DispatchToSaga(id, message, isInitiator, false, (SagaBase)handler, delegate(SagaBase saga)
                {
                    mhi.HandleMethod(saga, message);
                });
                if (b == Sagas.SagaStateHelper.SagaDispatchResult.ConcurrentUpdateHandleLater)
                {
                    //bus.HandleCurrentMessageLater(DateTime.Now.AddSeconds(1));
                    //throw new Exception("Should not happen");
                    throw new RetryMessageProcessingException(DateTime.Now.AddSeconds(1), "saga locked");
                }
                return true;
            }
            else
            {
                mhi.HandleMethod(handler, message);
                return true;
            }
        }


        public virtual void DispatchMessage(object message, IMessageBus bus)
        {
            bool found = false;

            foreach (ICustomMessageHandler mh in ServiceLocator.GetAllInstances<ICustomMessageHandler>())
            {
                if (!mh.PreHandle(message))
                {
                    //stop processing
                    log.Debug("Message dispatch cancelled by custom handler: {0}", mh.GetType());
                    return;
                }
                found = true;
            }

            Type tp = message.GetType();
            Type[] interfs = tp.GetInterfaces(); 
            foreach (Type interfType in interfs) //dispatch based on message interfaces
            {
                if (typeof(IMessageInterface).IsAssignableFrom(interfType))
                {
                    MsgHandlerInfo mhi = GetHandlersFor(interfType);
                    if (mhi != null)
                    {
                        foreach (object hnd in ServiceLocator.GetAllInstances(mhi.MessageHandlerGenericType))
                        {
                            try
                            {
                                CallHandler(message, hnd, mhi, bus);
                                found = true;
                            }
                            catch (TargetInvocationException ti)
                            {
                                log.Error("Error invoking message handler {0} for message {1}: {2}", hnd.GetType(), message.GetType(), ti.InnerException);
                                throw;
                            }
                        }
                    }
                }
            }

            while (tp.BaseType != null) //dispatch based on message type
            {
                MsgHandlerInfo mhi = GetHandlersFor(tp);
                if (mhi != null)
                {
                    foreach (object hnd in ServiceLocator.GetAllInstances(mhi.MessageHandlerGenericType))
                    {
                        try
                        {
                            CallHandler(message, hnd, mhi, bus);
                            found = true;
                        }
                        catch (TargetInvocationException ti)
                        {
                            log.Error("Error invoking message handler {0} for message {1}: {2}", hnd.GetType(), message.GetType(), ti.InnerException);
                            throw;
                        }
                    }
                }
                tp = tp.BaseType;
            }

            foreach (ICustomMessageHandler mh in ServiceLocator.GetAllInstances<ICustomMessageHandler>())
            {
                mh.PostHandle(message);
            }

            if (!found)
            {
                //log.Error("No handler for message: {0}", message.ToString());
                if (RequireHandler)
                    throw new Exception("No message handler for " + message.GetType().FullName);
            }
        }
    }
}
