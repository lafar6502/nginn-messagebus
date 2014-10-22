using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Transactions;
using System.IO;
using NLog;


namespace NGinnBPM.MessageBus.Impl
{
    /// <summary>
    /// Message bus implementation
    /// </summary>
    public class MessageBus : IMessageBus
    {
        private IMessageTransport _transport;
        
        private Logger log = LogManager.GetCurrentClassLogger();
        /// <summary>
        /// Serializer used for serializing message body
        /// </summary>
        protected ISerializeMessages MessageSerializer { get; set; }
        /// <summary>
        /// Message dispatcher used for delivering messages to their handlers
        /// </summary>
        protected IMessageDispatcher Dispatcher { get; set; }
		
		///Handle messages inside Microsoft.Transactions.TransactionScope
		public bool UseTransactionScope { get; set; }
        
        /// <summary>
        /// Service locator
        /// currently used for locating message transports when
        /// forwarding messages to remote endpoint
        /// </summary>
        protected IServiceResolver ServiceLocator { get; set; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="transport">Message transport used as a backend for this message bus</param>
        public MessageBus(IMessageTransport transport, IMessageDispatcher dispatcher, ISerializeMessages serializer, IServiceResolver serviceResolver)
        {
            log = LogManager.GetLogger("BUS_" + transport.Endpoint);
            log.Info("Message Bus {0} created", transport.Endpoint);
            MessageSerializer = serializer;
            Dispatcher = dispatcher;
            ServiceLocator = serviceResolver;
            _transport = transport;
            _transport.OnMessageArrived += new MessageArrived(_transport_OnMessageArrived);
            _transport.OnMessageToUnknownDestination += new MessageArrived(_transport_OnMessageToUnknownDestination);
            SubscriptionService = new DummySubscriptionService();
            BatchOutgoingMessagesInTransaction = true;
            MessageHandlerTransactionScopeOption = TransactionScopeOption.Required;
			UseTransactionScope = true;
            DefaultSubscriptionLifetime = TimeSpan.FromHours(48);
            PublishLocalByDefault = true;
        }

        void _transport_OnMessageToUnknownDestination(MessageContainer message, IMessageTransport transport)
        {
            string dest = message.To;
            log.Debug("Message to remote destination {0}", dest);
            Uri uri = new Uri(dest);
            string sn = string.Format("MessageTransport_{0}", uri.Scheme);
            IMessageTransport mt = ServiceLocator.GetInstance<IMessageTransport>(sn);
            if (mt == null) throw new Exception("No message transport configured for destination " + dest);
            mt.Send(message);
        }

        void _transport_OnMessageArrived(MessageContainer message, IMessageTransport transport)
        {
            Debug.Assert(message.BodyStr != null && message.Body == null);
            List<Action<MessageContainer, Exception>> callbacks = null;
            MessagePreprocessResult disp = PreprocessIncomingMessage(message, transport, out callbacks);
            if (disp == MessagePreprocessResult.CancelFurtherProcessing)
            {
                return; 
            }
            Exception e2 = null;
            try
            {
                DeserializeMessage(message);
                Debug.Assert(message.Body != null);
                DispatchIncomingMessage(message);
            }
            catch (Exception ex)
            {
                e2 = ex;
                throw;
            }
            finally
            {
                if (callbacks != null)
                {
                    try
                    {
                        callbacks.Reverse();
                        callbacks.ForEach(x => x(message, e2));
                    }
                    catch (Exception e3)
                    {
                        log.Warn("Callback error: {0}", e3);
                    }
                }
            }
        }

        protected virtual void DeserializeMessage(MessageContainer mc)
        {
            Debug.Assert(mc.Body == null);
            mc.Body = this.MessageSerializer.Deserialize(new StringReader(mc.BodyStr));
        }

        /// <summary>
        /// Transaction mode for message handlers
        /// By default all handlers are run in a transaction.
        /// </summary>
        public TransactionScopeOption MessageHandlerTransactionScopeOption { get; set; }

        /// <summary>
        /// current message information
        /// </summary>
        [ThreadStatic]
        private static CurMsg _currentMessage;

        protected virtual MessagePreprocessResult PreprocessIncomingMessage(MessageContainer mc, IMessageTransport t, out List<Action<MessageContainer, Exception>> callbacks)
        {
            List<Action<MessageContainer, Exception>> callb = null;
            callbacks = callb;
            //todo: allow for ordering of message preprocessors
            foreach (IPreprocessMessages pm in this.ServiceLocator.GetAllInstances<IPreprocessMessages>())
            {
                Action<MessageContainer, Exception> act;
                var res = pm.HandleIncomingMessage(mc, t, out act);
                ServiceLocator.ReleaseInstance(pm);
                if (res != MessagePreprocessResult.ContinueProcessing) return res;
                if (act != null)
                {
                    if (callb == null) callb = new List<Action<MessageContainer, Exception>>();
                    callb.Add(act);
                }
            }
            callbacks = callb;
            return MessagePreprocessResult.ContinueProcessing;
        }
        /// <summary>
        /// Deliver the message to handlers
        /// </summary>
        /// <param name="mc"></param>
        protected virtual void DispatchIncomingMessage(MessageContainer mc)
        {
            log.Debug("MB {2} Dispatching incoming message {0}/{1}/{3}", mc.To, mc.BusMessageId, this.Endpoint, mc.Body.GetType().Name);
            try
            {
                MessageBusContext.CurrentMessageBus = this;
                _currentMessage = new CurMsg(mc);
                if (UseTransactionScope && Transaction.Current == null)
                {
                    TransactionOptions to = new TransactionOptions();
                    to.IsolationLevel = IsolationLevel.ReadCommitted;
                    to.Timeout = TimeSpan.FromSeconds(30);
                    TransactionScopeOption tso = MessageHandlerTransactionScopeOption;
                    using (TransactionScope ts = new TransactionScope(tso, to))
                    {
                        Dispatcher.DispatchMessage(mc.Body, this);
                        ts.Complete();
                    }
                }
                else
                {
                    Dispatcher.DispatchMessage(mc.Body, this);
                }
            }
            finally
            {
                _currentMessage = null;
                MessageBusContext.CurrentMessageBus = null;
            }
        }
        
        /// <summary>
        /// Transport used by the message bus
        /// </summary>
        public IMessageTransport MessageTransport
        { 
            get { return _transport; } 
        }
        
        /// <summary>
        /// Subscription service
        /// </summary>
        public ISubscriptionService SubscriptionService { get; set; }

        /// <summary>
        /// true if all messages sent in a transaction scope should be batched 
        /// and sent in one message transport call
        /// </summary>
        public bool BatchOutgoingMessagesInTransaction { get; set; }
        
        /// <summary>
        /// By default publish messages to local endpoint and all subscribers.
        /// If false, local endpoint will be included only if subscriber config says so
        /// </summary>
        public bool PublishLocalByDefault { get; set; }

        private Guid _seed = Guid.NewGuid();
        private int _idcnt = 1;
        /// <summary>
        /// Allocates a new message unique ID
        /// </summary>
        /// <returns></returns>
        protected string CreateNewMessageUniqueId()
        {
            int id = System.Threading.Interlocked.Increment(ref _idcnt);
            return string.Format("{0}{1}", _seed.ToString("N"), id);
        }

        #region IMessageBus Members

        public void Notify(object msg)
        {
            Notify(new object[] { msg });
        }

        public void Notify(object[] msgs)
        {
            Notify(msgs, DeliveryMode.DurableAsync);
        }

        private void Notify(object[] msgs, DeliveryMode mode)
        {
            foreach (var msg in msgs)
            {
                Dispatcher.DispatchMessageToOutgoingMessageHandlers(msg, this);
            }
            List<MessageContainer> lst = new List<MessageContainer>();
            foreach (Object obj in msgs)
            {
                MessageContainer mc = new MessageContainer();
                mc.From = Endpoint;
                mc.To = Endpoint;
                mc.Body = obj;
                mc.UniqueId = CreateNewMessageUniqueId();
                lst.Add(mc);
            }
            if (mode == DeliveryMode.DurableAsync)
            {
                NotifyAndDistributeMessages(lst, null);
            }
            else
            {
                lst.ForEach(mc => this.DispatchLocalNonPersistentMessage(mc, mode));
            }
        }

        /// <summary>
        /// Return list of queue names where message of specified type should be published
        /// this selects all remote subscribers of specified message type (or its base classes)
        /// and a local endpoint if there is any subscriber for specified message type.
        /// </summary>
        /// <param name="msgType"></param>
        /// <returns></returns>
        public virtual string[] GetTargetQueuesForMessageType(Type msgType)
        {
            Type tp = msgType;
            HashSet<string> set = new HashSet<string>();
            while (tp != null)
            {
                IEnumerable<string> ends = SubscriptionService.GetTargetEndpoints(tp.FullName);
                foreach (string endp in ends)
                {
                    var s = endp;
                    if (endp.Equals("local", StringComparison.InvariantCultureIgnoreCase)) s = Endpoint;
                    if (!set.Contains(s)) set.Add(s);
                }
                tp = tp.BaseType;
            }
            if (!set.Contains(Endpoint))
            {
                if (PublishLocalByDefault) 
                    set.Add(Endpoint);
                else if (Dispatcher.HasHandlerFor(msgType))
                    set.Add(Endpoint);
            }
            return set.ToArray();
        }

        /// <summary>
        /// This method dispatches the message directly to their handlers (local only)
        /// without persisting it in database and in a non-transactional way.
        /// Effectively bypasses NGinn MessageBus, just forwards the message to handler components
        /// </summary>
        /// <param name="mc"></param>
        internal void DispatchLocalNonPersistentMessage(MessageContainer mc, DeliveryMode mode)
        {
            if (mc.To != Endpoint) throw new Exception("Local only please");
            if (mc.From != Endpoint) throw new Exception("Local only please");
            if (mc.DeliverAt > DateTime.Now) throw new Exception("Delivery date unsupported");
            if (mc.Cc != null)
            {
                foreach (string s in mc.Cc) if (s != Endpoint) throw new Exception("Non-local endpoint on cc");
            }
            if (mode == DeliveryMode.LocalAsync)
            {
                System.Threading.ThreadPool.QueueUserWorkItem(new System.Threading.WaitCallback(delegate(object v)
                {
                    DispatchLocal(mc, mode);
                }));
            }
            else
            {
                DispatchLocal(mc, mode);
            }
        }

        private void DispatchLocal(MessageContainer mc, DeliveryMode dm)
        {
            var pm = _currentMessage;
            var mb = MessageBusContext.CurrentMessageBus;
            try
            {
                _currentMessage = new CurMsg(mc);
                MessageBusContext.CurrentMessageBus = this;
                _currentMessage.DeliveryMode = dm;
                Dispatcher.DispatchMessage(mc.Body, this);
            }
            catch (Exception ex)
            {
                log.Warn("Error async processing message {0}: {1}", mc.BusMessageId, ex.ToString());
                if (dm != DeliveryMode.LocalAsync)
                {
                    throw;
                }
            }
            finally
            {
                _currentMessage = pm;
                MessageBusContext.CurrentMessageBus = mb;
            }
        }
        /// <summary>
        /// Distribute messages in the list to their destination subscriber endpoints
        /// and send them all
        /// </summary>
        /// <param name="msgs"></param>
        internal void NotifyAndDistributeMessages(IList<MessageContainer> msgs, object dbTran)
        {
            List<MessageContainer> lst = new List<MessageContainer>();
            foreach (MessageContainer msg in msgs)
            {
                msg.From = Endpoint;
                if (msg.Body == null) throw new NullReferenceException("Message body is null");
                string[] targets = GetTargetQueuesForMessageType(msg.Body.GetType());
                if (targets == null || targets.Length == 0)
                {
                    log.Debug("No destination queue for message {0}", msg.Body.GetType().Name);
                    continue;
                }
                msg.To = targets[0];
                lst.Add(msg);
                for (int i = 1; i < targets.Length; i++)
                {
                    MessageContainer mw2 = msg.Clone() as MessageContainer;
                    mw2.UniqueId = CreateNewMessageUniqueId();
                    mw2.To = targets[i];
                    lst.Add(mw2);
                }
            }
            SendMessages(lst, dbTran);
        }


        public void Send(string destination, object msg)
        {
            Send(destination, new object[] { msg });
        }

        public void Send(string destination, object[] msgs)
        {
            foreach (var msg in msgs)
            {
                Dispatcher.DispatchMessageToOutgoingMessageHandlers(msg, this);
            }
            if (destination == null)
                destination = Endpoint;
            List<MessageContainer> lst = new List<MessageContainer>();
            foreach (object obj in msgs)
            {
                MessageContainer mc = new MessageContainer();
                mc.From = MessageTransport.Endpoint;
                mc.To = destination;
                mc.UniqueId = CreateNewMessageUniqueId();
                mc.Body = obj;
                lst.Add(mc);
            }
            SendMessages(lst, null);
        }

        /// <summary>
        /// Send messages using underlying transport.
        /// Serializes message bodies, adds unique ids
        /// </summary>
        /// <param name="lst"></param>
        /// <param name="dbTran">External transaction to be used for sending messages (optional)</param>
        internal void SendMessages(IList<MessageContainer> lst, object dbTran)
        {
            if (lst == null || lst.Count == 0) return;
            object prevbody = null;
            string prevstr = null;
            foreach (MessageContainer mc in lst)
            {
                if (string.IsNullOrEmpty(mc.To)) throw new Exception("Message destination missing");
                if (mc.Body == null) throw new NullReferenceException("Message body is null");
                if (mc.From == null)
                    mc.From = MessageTransport.Endpoint;
                if (mc.UniqueId == null || mc.UniqueId.Length == 0)
                    mc.UniqueId = CreateNewMessageUniqueId();
                if (mc.Body == prevbody)
                {
                    mc.BodyStr = prevstr;
                }
                else
                {
                    prevbody = mc.Body;
                    StringWriter sw = new StringWriter();
                    MessageSerializer.Serialize(mc.Body, sw);
                    //MessageSerializer.Serialize(mc.Body, sw);
                    mc.BodyStr = sw.ToString();
                    prevstr = mc.BodyStr;
                }
            }
            if (Transaction.Current == null || BatchOutgoingMessagesInTransaction == false)
            {
                MessageTransport.SendBatch(lst, dbTran);
            }
            else
            {
                MessageBatchingRM rm = GetCreateMessageBatchForCurrentTransaction();
                if (!rm.TransactionOpen)
                    throw new Exception("Transaction is not open: " + rm.TransactionId);

                foreach (MessageContainer mw in lst)
                {
                    rm.Messages.Add(mw);
                }
            }
        }

        public void NotifyAt(DateTime deliverAt, object msg)
        {
            NewMessage(msg).SetDeliveryDate(deliverAt).Publish();
        }

        public void SendAt(DateTime deliverAt, string destination, object msg)
        {
            NewMessage(msg).SetDeliveryDate(deliverAt).Send(destination);
        }

        public void Reply(object msg)
        {
            if (_currentMessage.DeliveryMode != DeliveryMode.DurableAsync)
            {
                NewMessage(msg)
                    .SetCorrelationId(CurrentMessageInfo.CorrelationId)
                    .SetDeliveryMode(_currentMessage.DeliveryMode)
                    .Publish();
            }
            else
            {
                string returnAddr = CurrentMessageInfo.Sender;
                if (CurrentMessageInfo.Headers != null && CurrentMessageInfo.Headers.ContainsKey(MessageContainer.HDR_ReplyTo))
                    returnAddr = CurrentMessageInfo.Headers[MessageContainer.HDR_ReplyTo];
                NewMessage(msg)
                    .SetCorrelationId(CurrentMessageInfo.CorrelationId)
                    .Send(returnAddr);
            }
        }

        public string Endpoint
        {
            get { return MessageTransport.Endpoint; }
        }

        public TimeSpan DefaultSubscriptionLifetime { get; set; }

        public void HandleCurrentMessageLater(DateTime howLater)
        {
            _transport.ProcessCurrentMessageLater(howLater);
        }

        public CurrentMessageInfo CurrentMessageInfo
        {
            get 
            {
                return _currentMessage;
            }
        }

        public void SubscribeAt(string endpoint, Type messageType)
        {
            Send(endpoint, new Messages.SubscribeRequest { MessageType = messageType.FullName, SubscriberEndpoint = Endpoint, SubscriptionTime = DefaultSubscriptionLifetime });
        }

        public void UnsubscribeAt(string endpoint, Type messageType)
        {
            Send(endpoint, new Messages.UnsubscribeRequest { MessageType = messageType.FullName, SubscriberEndpoint = Endpoint });
        }

        public ISendMessage NewMessage()
        {
            return new NewMessageFluent(this);
        }

        public ISendMessage NewMessage(object body)
        {
            return new NewMessageFluent(this).SetBody(body);
        }

        #endregion

        
        /// <summary>
        /// Dictionary transactionId->Message batch, for batching sends in transactions
        /// </summary>
        private Dictionary<string, MessageBatchingRM> _messageBatches = new Dictionary<string, MessageBatchingRM>();

        
        public string GetCurrentTransactionState()
        {
        	var rm = this.GetCurrentTransactionMessageBatch();
        	return  Newtonsoft.Json.JsonConvert.SerializeObject(rm.Messages);
        }
        
        public void SetCurrentTransactionState(string state)
        {
        	var rm = this.GetCurrentTransactionMessageBatch();
        	rm.Messages = Newtonsoft.Json.JsonConvert.DeserializeObject<List<MessageContainer>>(state);
        }
        
        internal MessageBatchingRM GetCurrentTransactionMessageBatch()
        {
        	if (Transaction.Current == null)
                throw new Exception("No transaction!");
            string tid = Transaction.Current.TransactionInformation.LocalIdentifier;
            MessageBatchingRM rm;
            lock (this)
            {
                if (_messageBatches.TryGetValue(tid, out rm)) return rm;
            }
            return null;
        }
        
        private MessageBatchingRM GetCreateMessageBatchForCurrentTransaction()
        {
            if (Transaction.Current == null)
                throw new Exception("No transaction!");
            string tid = Transaction.Current.TransactionInformation.LocalIdentifier;
            MessageBatchingRM rm;
            lock (this)
            {
                if (_messageBatches.TryGetValue(tid, out rm))
                    return rm;
                log.Debug("Enlisting resource manager for {0}", tid);
                rm = new MessageBatchingRM(r => MessageTransport.SendBatch(r.Messages, null), CommitMessageBatch, RollbackMessageBatch);
                rm.TransactionId = tid;
                _messageBatches[tid] = rm;
            }
            Transaction.Current.EnlistVolatile(rm, EnlistmentOptions.None);
            return rm;
        }

        private void CommitMessageBatch(MessageBatchingRM rm)
        {
            lock (this)
            {
                _messageBatches.Remove(rm.TransactionId);
            }
            log.Debug("Removed message batch {0}", rm.TransactionId);
        }

        private void RollbackMessageBatch(MessageBatchingRM rm)
        {
            lock (this)
            {
                _messageBatches.Remove(rm.TransactionId);
            }
            log.Debug("Removed message batch {0}", rm.TransactionId);
        }


        

        [ThreadStatic]
        private static object _appCon;

        public static object CurrentApplicationManagedConnection
        {
            get { return _appCon; }
            set { _appCon = value; }
        }


        public void Notify(object msg, DeliveryMode mode)
        {
            Notify(new object[] {msg}, mode);
        }
    }

    /// <summary>
    /// Fluent message builder
    /// </summary>
    internal class NewMessageFluent : ISendMessage
    {
        private MessageBus mbus;
        private MessageContainer mc;
        private List<object> _bodies = new List<object>();

        public NewMessageFluent(MessageBus bus)
        {
            mbus = bus;
            mc = new MessageContainer();
            mc.From = mbus.Endpoint;
        }



        #region IConfigMessage Members

        public ISendMessage SetBody(object body)
        {
            _bodies.Clear();
            _bodies.Add(body);
            return this;
        }

        public ISendMessage SetUniqueId(string id)
        {
            mc.UniqueId = id;
            return this;
        }

        public ISendMessage SetCorrelationId(string id)
        {
            mc.CorrelationId = id;
            return this;
        }

        public ISendMessage SetDeliveryDate(DateTime dt)
        {
            mc.DeliverAt = dt;
            return this;
        }

        public ISendMessage SetTTL(DateTime ttl)
        {
            AddHeader(MessageContainer.HDR_TTL, ttl.ToString("yyyy-MM-dd HH:mm:ss"));
            return this;
        }

        public ISendMessage InSequence(string sequenceId, int seqNumber, int? seqLen)
        {
            if (seqNumber < 0) throw new ArgumentException("seqNumber");
            if (seqLen.HasValue && seqLen.Value < 0) throw new ArgumentException("seqLen");
            if (seqLen.HasValue && seqLen.Value <= seqNumber) throw new ArgumentException("seqNumber higher than seqLen");

            AddHeader(MessageContainer.HDR_SeqId, sequenceId);
            AddHeader(MessageContainer.HDR_SeqNum, seqNumber.ToString());
            if (seqLen.HasValue)
                AddHeader(MessageContainer.HDR_SeqLen, seqLen.Value.ToString());
            return this;
        }

        public ISendMessage AddHeader(string name, string value)
        {
            mc.SetHeader(name, value);
            return this;
        }

        public void Send(string endpoint)
        {
            if (_deliveryMode != DeliveryMode.DurableAsync)
            {
                if (endpoint != mbus.Endpoint) throw new Exception("Only local endpoint allowed with this delivery mode");
                foreach (object body in _bodies)
                {
                    mc.Body = body;
                    mbus.DispatchLocalNonPersistentMessage(mc, _deliveryMode);
                }
            }
            else
            {
                List<MessageContainer> l = new List<MessageContainer>();
                foreach (object body in _bodies)
                {
                    MessageContainer mc2 = mc.Clone() as MessageContainer;
                    mc2.To = endpoint;
                    mc2.Body = body;
                    l.Add(mc2);
                }
                mbus.SendMessages(l, _dbTran);
            }
        }

        public void Publish()
        {
            
            mc.To = mbus.Endpoint;
            if (_deliveryMode != DeliveryMode.DurableAsync)
            {
                foreach (object body in _bodies)
                {
                    mc.Body = body;
                    mbus.DispatchLocalNonPersistentMessage(mc, _deliveryMode);
                }
            }
            else
            {
                List<MessageContainer> l = new List<MessageContainer>();
                foreach (object body in _bodies)
                {
                    MessageContainer mc2 = mc.Clone() as MessageContainer;
                    mc2.Body = body;
                    l.Add(mc2);
                }
                
                mbus.NotifyAndDistributeMessages(l, _dbTran);
            }
        }


        public ISendMessage SetLabel(string label)
        {
            if (label.Length > 100)
                label = label.Substring(0, 100);
            mc.SetHeader(MessageContainer.HDR_Label, label);
            return this;
        }

        #endregion

        #region ISendMessage Members


        public ISendMessage AlsoSendTo(string endpoint)
        {
            if (mc.Cc == null)
                mc.Cc = new List<string>();
            if (!mc.Cc.Contains(endpoint))
                mc.Cc.Add(endpoint);
            return this;
        }

        #endregion

        #region ISendMessage Members


        public ISendMessage ReplyTo(string endpoint)
        {
            mc.SetHeader(MessageContainer.HDR_ReplyTo, endpoint);
            return this;
        }

        private DeliveryMode _deliveryMode = DeliveryMode.DurableAsync;
        

        #endregion


        public ISendMessage SetBatch(object[] messages)
        {
            _bodies.Clear();
            _bodies.AddRange(messages);
            return this;
        }

        private object _dbTran = null;
        public ISendMessage UseConnection(object tran)
        {
            _dbTran = tran;
            return this;
        }


        public ISendMessage MarkHighPriority()
        {
            this.mc.HiPriority = true;
            return this;
        }


        public ISendMessage SetDeliveryMode(DeliveryMode b)
        {
            this._deliveryMode = b;
            return this;
        }
    }

    /// <summary>
    /// Current message information provider
    /// </summary>
    internal class CurMsg : CurrentMessageInfo
    {
        internal CurMsg(MessageContainer mc)
        {
            Message = mc;
            DeliveryMode = DeliveryMode.DurableAsync;
        }


        public object Body
        {
            get { return Message.Body; }
        }

        public string Sender
        {
            get { return Message.From; }
        }

        public string Destination
        {
            get { return Message.To; }
        }

        public string MessageId
        {
            get { return Message.BusMessageId; }
        }

        public string MessageUniqueId
        {
            get { return Message.UniqueId; }
        }

        public string CorrelationId
        {
            get { return Message.CorrelationId; }
        }

        public IDictionary<string, string> Headers
        {
            get { return Message.Headers; }
        }

        public MessageContainer Message { get; set; }

        /// <summary>
        /// current message's delivery mode
        /// </summary>
        public DeliveryMode DeliveryMode { get; set; }

        public bool IsFinalRetry
        {
            get { return Message.IsFinalRetry; }
        }
        
    }
}
