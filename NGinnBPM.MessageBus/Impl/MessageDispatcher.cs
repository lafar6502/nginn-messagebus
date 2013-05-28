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
        private Logger log;
        /// <summary>
        /// for delivering messages to sagas...
        /// </summary>
        public Sagas.SagaStateHelper SagaHandler { get; set; }

        public MessageDispatcher()
        {
            log = LogManager.GetLogger("MessageDispatcher_" + this.GetHashCode());
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
            //not necessary as we don't cache handler instances
            //this._handlers = new Dictionary<Type, MsgHandlerInfo>();
        }


        protected virtual bool CallHandler(object message, object handler, MsgHandlerInfo mhi, IMessageBus bus)
        {
            log.Debug("Call Handler");
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
                log.Debug("Calling mhi.HandleMethod");
                mhi.HandleMethod(handler, message);
                return true;
            }
        }

        protected virtual bool GetAllHandlersForMessageType(Type t, out ICollection<object> handlers, out MsgHandlerInfo handlerInfo)
        {
            handlers = null; 
            handlerInfo = GetHandlersFor(t);
            if (handlerInfo == null) return false;
            handlers = ServiceLocator.GetAllInstances(handlerInfo.MessageHandlerGenericType);
            var nh = handlerInfo._numHandlersFound;
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

        public virtual void DispatchMessage(object message, IMessageBus bus)
        {
            RequireHandler = true;
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
            log.Debug("Dispatching: message type is " + tp.AssemblyQualifiedName);
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
                        }
                    }
                }
            }

            while (tp != null) //dispatch based on message type
            {
            	log.Debug("Calling GetHandlersFor({0})", tp.AssemblyQualifiedName);
                MsgHandlerInfo mhi;
                ICollection<object> handlers;
                if (GetAllHandlersForMessageType(tp, out handlers, out mhi))
                {
                    //log.Debug("Found Handler " + mhi.MessageHandlerGenericType.Name);
                    log.Debug("Found {0} handler instances for type {1}", handlers.Count, tp.AssemblyQualifiedName);
                    var allInstances = ServiceLocator.GetAllInstances(mhi.MessageHandlerGenericType);
                    //RG handlers and instances are same thing: log.Debug(string.Format("found {0} handlers with {1} instances",handlers.Count,allInstances.Count));
                    if (handlers.Count != allInstances.Count) log.Error("Handler Mismatch!");
                    foreach (object hnd in handlers)
                    {
                        log.Debug("calling handler instance {0}", hnd.GetType().AssemblyQualifiedName);
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
                else
                {
                    log.Debug("No Handler");
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
