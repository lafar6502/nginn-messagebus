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
    public class MessageDispatcher : IMessageDispatcher
    {

        public bool RequireHandler { get; set; }
        protected IServiceResolver ServiceLocator { get; private set; }
        private Logger log;
        /// <summary>
        /// for delivering messages to sagas...
        /// </summary>
        public Sagas.SagaStateHelper SagaHandler { get; set; }

        public MessageDispatcher(IServiceResolver resolver)
        {
            ServiceLocator = resolver;
            log = LogManager.GetLogger("MessageDispatcher_" + this.GetHashCode());
            RequireHandler = false;
        }

        public class MsgHandlerInfo
        {
            public Type MessageHandlerGenericType;
            public Type InitiatedByGenericType;
            public Type MessageType;
            public MessageHandlerMethodDelegate HandleMethod;
            public int? _numHandlersFound;
        }

        private Dictionary<Type, MsgHandlerInfo> _handlers = new Dictionary<Type, MsgHandlerInfo>();
        private Dictionary<Type, MsgHandlerInfo> _outHandlers = new Dictionary<Type, MsgHandlerInfo>();

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

        protected MsgHandlerInfo GetOutHandlersFor(Type msgType)
        {
            MsgHandlerInfo mi;
            if (_outHandlers.TryGetValue(msgType, out mi))
                return mi;
            lock (this)
            {
                if (_outHandlers.TryGetValue(msgType, out mi))
                    return mi;
                mi = new MsgHandlerInfo();
                mi.MessageType = msgType;
                mi.MessageHandlerGenericType = typeof(IOutgoingMessageHandler<>).MakeGenericType(msgType);
                mi.InitiatedByGenericType = typeof(IOutgoingMessageHandler<>).MakeGenericType(msgType);
                mi.HandleMethod = DelegateFactory.CreateMessageHandlerDelegate(mi.MessageHandlerGenericType.GetMethod("OnMessageSend"));
                _outHandlers[msgType] = mi;
                return mi;
            }
        }

        /// <summary>
        /// Call this method when message handlers have been modified
        /// </summary>
        public void HandlerConfigurationChanged()
        {
            //not necessary as we don't cache handler instances
            //this._handlers = new Dictionary<Type, MsgHandlerInfo>();
        }


        protected virtual bool CallHandler(object message, object handler, MsgHandlerInfo mhi, IMessageBus bus)
        {
            if (handler is SagaBase)
            {
                if (SagaHandler == null) throw new PermanentMessageProcessingException("Saga support has not been enabled");
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

        public virtual bool HasHandlerFor(Type messageType)
        {
            var tp = messageType;
            while (tp != null) //dispatch based on message type
            {
                var ht = GetHandlersFor(tp).MessageHandlerGenericType;
                if (ServiceLocator.HasService(ht)) return true;
                tp = tp.BaseType;
            }

            Type[] interfs = messageType.GetInterfaces();
            foreach (Type interfType in interfs) //dispatch based on message interfaces
            {
                if (typeof(IMessageInterface).IsAssignableFrom(interfType))
                {
                    var ht = GetHandlersFor(interfType).MessageHandlerGenericType;
                    if (ServiceLocator.HasService(ht)) return true;
                }
            }
            return false;
        }

        protected virtual bool GetAllHandlersForMessageType(Type t, out ICollection<object> handlers, out MsgHandlerInfo handlerInfo)
        {
            handlers = null; 
            handlerInfo = GetHandlersFor(t);
            if (handlerInfo == null) return false;
            var nh = handlerInfo._numHandlersFound;
            handlers = ServiceLocator.GetAllInstances(handlerInfo.MessageHandlerGenericType);
            if (nh.HasValue)
            {
                //yeah, I know this is not thread safe but it's supposed to be a harmless sanity check
                if (nh.Value != handlers.Count)
                {
                    log.Warn("Number of handlers changed for type {0}. Was {1}, now is {2}", t.FullName, nh.Value, handlers.Count);
                    handlerInfo._numHandlersFound = handlers.Count;
                }
            }
            else handlerInfo._numHandlersFound = handlers.Count;
            return true;
        }

        protected virtual bool GetOutgoingHandlersForMessageType(Type t, out ICollection<object> handlers, out MsgHandlerInfo handlerInfo)
        {
            handlers = null;
            handlerInfo = GetOutHandlersFor(t);
            if (handlerInfo == null) return false;
            handlers = ServiceLocator.GetAllInstances(handlerInfo.MessageHandlerGenericType);
            return true;
        }

        public virtual void DispatchMessage(object message, IMessageBus bus)
        {
            bool found = false;

            foreach (ICustomMessageHandler mh in ServiceLocator.GetAllInstances<ICustomMessageHandler>())
            {
                var b = mh.PreHandle(message);
                ServiceLocator.ReleaseInstance(mh);
                if (!b)
                {
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
                    MsgHandlerInfo mhi;
                    ICollection<object> handlers;
                    if (GetAllHandlersForMessageType(interfType, out handlers, out mhi))
                    {
                        foreach (object hnd in handlers)
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
                            ServiceLocator.ReleaseInstance(hnd);
                        }
                    }
                }
            }

            while (tp != null) //dispatch based on message type
            {
                MsgHandlerInfo mhi;
                ICollection<object> handlers;
                if (GetAllHandlersForMessageType(tp, out handlers, out mhi))
                {
                    foreach (object hnd in handlers)
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
                        ServiceLocator.ReleaseInstance(hnd);
                    }
                }
                tp = tp.BaseType;
            }

            foreach (ICustomMessageHandler mh in ServiceLocator.GetAllInstances<ICustomMessageHandler>())
            {
                mh.PostHandle(message);
                ServiceLocator.ReleaseInstance(mh);
            }

            if (!found)
            {
                log.Debug("No handler for message: {0}", message.ToString());
                if (RequireHandler)
                    throw new Exception("No message handler for " + message.GetType().FullName);
            }
        }


        public void DispatchMessageToOutgoingMessageHandlers(object message, IMessageBus bus)
        {
            Type tp = message.GetType();
            while (tp != null) //dispatch based on message type
            {
                MsgHandlerInfo mhi;
                ICollection<object> handlers;
                if (GetOutgoingHandlersForMessageType(tp, out handlers, out mhi))
                {
                    foreach (object hnd in handlers)
                    {
                        try
                        {
                            mhi.HandleMethod(hnd, message);
                        }
                        catch (TargetInvocationException ti)
                        {
                            log.Error("Error invoking message handler {0} for message {1}: {2}", hnd.GetType(), message.GetType(), ti.InnerException);
                            throw;
                        }
                        ServiceLocator.ReleaseInstance(hnd);
                    }
                }
                tp = tp.BaseType;
            }
            Type[] interfs = message.GetType().GetInterfaces();
            foreach (Type interfType in interfs) //dispatch based on message interfaces
            {
                if (typeof(IMessageInterface).IsAssignableFrom(interfType))
                {
                    MsgHandlerInfo mhi;   
                    ICollection<object> handlers;
                    if (GetOutgoingHandlersForMessageType(interfType, out handlers, out mhi))
                    {
                        foreach (object hnd in handlers)
                        {
                            try
                            {
                                mhi.HandleMethod(hnd, message);
                            }
                            catch (TargetInvocationException ti)
                            {
                                log.Error("Error invoking message handler {0} for message {1}: {2}", hnd.GetType(), message.GetType(), ti.InnerException);
                                throw;
                            }
                            ServiceLocator.ReleaseInstance(hnd);
                        }
                    }
                }
            }
        }
    }
}
