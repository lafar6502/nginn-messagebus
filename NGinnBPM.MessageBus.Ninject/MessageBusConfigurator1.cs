using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Ninject;
using NGinnBPM.MessageBus.Impl;
using Ninject.Selection.Heuristics;
using NLog;

namespace NGinnBPM.MessageBus.NinjectConfig
{
    public class MessageBusConfigurator
    {
        private IKernel _k;
        private IDictionary<string, string> _connStrings = new Dictionary<string, string>();
        private static Logger log = LogManager.GetCurrentClassLogger();

        private bool _useSqlOutputClause = false;

        public TimeSpan SubscriptionLifetime { get; set; }
        public bool BatchOutMessages { get; set; }
        public bool AutoCreateQueues { get; set; }
        public TimeSpan TransactionTimeout { get; set; }
        public bool AlwaysPublishLocal { get; set; }
        public bool EnableSagas { get; set; }
        private TimeSpan[] _retryTimes = new TimeSpan[] {
            TimeSpan.FromSeconds(30),
            TimeSpan.FromMinutes(3),
            TimeSpan.FromMinutes(20),
            TimeSpan.FromHours(2),
            TimeSpan.FromHours(8),
            TimeSpan.FromHours(24),
            TimeSpan.FromDays(3)
        };

        protected MessageBusConfigurator(IKernel k)
        {
            _k = k;
            EnableSagas = false;
            AlwaysPublishLocal = true;
            BatchOutMessages = true;
            ReuseReceiveConnectionForSending = true;
            ExposeReceiveConnectionToApplication = true;
            MessageRetentionPeriod = TimeSpan.FromDays(10);
            this.MaxConcurrentReceivers = 4;
            this.AutoStart = false;
            this.UseAppManagedConnectionForSending = true;
            this.UseTransactionScope = true;
            AutoCreateQueues = true;
            TransactionTimeout = TimeSpan.FromMinutes(1);
            SubscriptionLifetime = TimeSpan.FromHours(48);
            InitDefaultServices();
        }

        public static MessageBusConfigurator Begin()
        {
            return new MessageBusConfigurator(new StandardKernel());
        }

        public IKernel Container
        {
            get { return _k; }
        }

        protected void InitDefaultServices()
        {
            
            _k.Components.Add<IInjectionHeuristic, DefaultPropertyInjectionPolicy>();
            if (!IsServiceRegistered(typeof(IServiceResolver)))
            {
                _k.Bind<IServiceResolver>().To<NinjectServiceResolver>();
            }
        }

        /// <summary>
        /// Add connection string alias
        /// </summary>
        /// <param name="alias"></param>
        /// <param name="connString"></param>
        /// <returns></returns>
        public MessageBusConfigurator AddConnectionString(string alias, string connString)
        {
            _connStrings.Add(alias, connString);
            return this;
        }

        public IDictionary<string, string> GetConnectionStrings()
        {
            return _connStrings;
        }

        /// <summary>
        /// Enable/disable batch sending of all outgoing messages in a transaction
        /// </summary>
        /// <param name="b"></param>
        /// <returns></returns>
        public MessageBusConfigurator BatchOutgoingMessages(bool b)
        {
            BatchOutMessages = b;
            return this;
        }

        /// <summary>
        /// Set the timeout for message receiving transaction.
        /// If the transaction takes longer than that it will be aborted. 
        /// By default the timeout is 1 minute.
        /// </summary>
        /// <param name="ts"></param>
        /// <returns></returns>
        public MessageBusConfigurator SetReceiveTransactionTimeout(TimeSpan ts)
        {
            TransactionTimeout = ts;
            return this;
        }

        /// <summary>
        /// Set all connection string aliases at once
        /// </summary>
        /// <param name="aliasToConnectionString"></param>
        /// <returns></returns>
        public MessageBusConfigurator SetConnectionStrings(IDictionary<string, string> aliasToConnectionString)
        {
            _connStrings = aliasToConnectionString;
            return this;
        }

        /// <summary>
        /// Set message bus endpoint name
        /// </summary>
        /// <param name="endpoint"></param>
        /// <returns></returns>
        public MessageBusConfigurator SetEndpoint(string endpoint)
        {
            Endpoint = endpoint;
            return this;
        }

        public string Endpoint { get; set; }


        /// <summary>
        /// By default publishing a message publishes it to local endpoint and all subscriber endpoints.
        /// If you set this to false messages will be published local only if such subscription is present.
        /// </summary>
        /// <param name="b"></param>
        /// <returns></returns>
        public MessageBusConfigurator SetAlwaysPublishLocal(bool b)
        {
            AlwaysPublishLocal = b;
            return this;
        }

        public MessageBusConfigurator AutoCreateDatabase(bool b)
        {
            AutoCreateQueues = b;
            return this;
        }

        private string GetDefaultConnectionString()
        {
            if (Endpoint == null || Endpoint.Length == 0) throw new Exception("Configure endpoint first");
            if (_connStrings == null || _connStrings.Count == 0) throw new Exception("Configure connection strings first");
            string alias, table;
            if (!Impl.SqlUtil.ParseSqlEndpoint(Endpoint, out alias, out table))
                throw new Exception("Invalid endpoint");
            string connstr;
            if (!_connStrings.TryGetValue(alias, out connstr))
                throw new Exception("Connection string not defined for alias: " + alias);
            return connstr;
        }

        /// <summary>
        /// Configure SQL subscription database
        /// Warning: configure subscription parameters (lifetime, cache expiration time)
        /// before calling this function.
        /// </summary>
        /// <returns></returns>
        public MessageBusConfigurator UseSqlSubscriptions()
        {
            string connstr = GetDefaultConnectionString();
            _wc.Register(Component.For<ISubscriptionService>()
                .ImplementedBy<NGinnBPM.MessageBus.Impl.SqlSubscriptionService>()
                .DependsOn(new
                {
                    ConnectionString = connstr,
                    AutoCreateSubscriptionTable = true,
                    Endpoint = Endpoint,
                    CacheExpiration = _subscriptionCacheTime
                })
                .LifeStyle.Singleton);
            return this;
        }

        /// <summary>
        /// Enable support for sagas (disabled by default).
        /// 
        /// </summary>
        /// <param name="enable"></param>
        /// <returns></returns>
        public MessageBusConfigurator SetEnableSagas(bool enable)
        {
            EnableSagas = enable;
            return this;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="configFile"></param>
        /// <returns></returns>
        public MessageBusConfigurator UseStaticMessageRouting(string configFile)
        {
            _wc.Register(Component.For<ISubscriptionService>()
                .ImplementedBy<StaticMessageRouting>().LifeStyle.Singleton
                .DependsOn(new
                {
                    ConfigFile = configFile
                }));
            return this;
        }

        /// <summary>
        /// Configure message bus to use message sequence repository
        /// stored in default database in NGinnMessageBus_Sequences table
        /// </summary>
        /// <returns></returns>
        public MessageBusConfigurator UseSqlSequenceManager()
        {
            string connstr = GetDefaultConnectionString();
            _wc.Register(Component.For<ISequenceMessages>()
                .ImplementedBy<SqlSequenceManager>().LifeStyle.Singleton
                .DependsOn(new
                {
                    AutoCreateTable = true,
                    SequenceTable = "NGinnMessageBus_Sequences"
                }));
            return this;
        }

        /// <summary>
        /// Set this to false to disable enclosing of message handler in System.Transactions.TransactionScope.
        /// Without transaction scope you get better performance but no global transaction handling.
        /// Transaction scope is enabled by default. Disable it only if necessary.
        /// </summary>
        /// <param name="use"></param>
        /// <returns></returns>
        public MessageBusConfigurator SetUseTransactionScope(bool use)
        {
            UseTransactionScope = use;
            return this;
        }

        public bool UseTransactionScope { get; set; }


        /// <summary>
        /// If set to true, receiving transaction will be used also for sending all messages
        /// that are sent 'inside' the receive transaction (that is, from within the handler of the received message). 
        /// This way, you will have a transactional receive and send without involving a distributed transaction and with better performance. 
        /// Works only with sql transport.
        /// By default, the receive transaction is not used and all messages are sent in separate transaction. You can enable it but currently its experimental.
        /// </summary>
        /// <param name="b"></param>
        /// <returns></returns>
        public MessageBusConfigurator SetReuseReceiveConnectionForSending(bool b)
        {
            ReuseReceiveConnectionForSending = b;
            return this;
        }

        public bool ReuseReceiveConnectionForSending { get; set; }

        public bool ExposeReceiveConnectionToApplication { get; set; }
        /// <summary>
        /// If true the db connection used for receiving a message will be exposed via
        /// Advanced.MessageBusCurrentThreadContext.ReceivingConnection
        /// True by default. 
        /// </summary>
        /// <param name="b"></param>
        /// <returns></returns>
        public MessageBusConfigurator SetExposeReceiveConnectionToApplication(bool b)
        {
            ExposeReceiveConnectionToApplication = b;
            return this;
        }

        public bool UseAppManagedConnectionForSending { get; set; }


        /// <summary>
        /// If true the message bus will try to use application-supplied db connection when
        /// sending messages. True by default.
        /// See Advanced.MessageBusCurrentThreadContext.AppManagedConnection
        /// </summary>
        /// <param name="b"></param>
        /// <returns></returns>
        public MessageBusConfigurator UseApplicationManagedConnectionForSending(bool b)
        {
            UseAppManagedConnectionForSending = b;
            return this;
        }

        protected MessageBusConfigurator ConfigureSqlMessageBus()
        {
            /*if (!IsServiceRegistered<DbInitialize>())
            {
                _k.Bind<DbInitialize>().ToSelf().InSingletonScope().OnActivation((c, x) =>
                {
                    x.ConnectionString = GetDefaultConnectionString();
                });
            }*/

            _k.Bind<IMessageTransport, IStartableService, IHealthCheck, SqlMessageTransport2>().To<SqlMessageTransport2>().InSingletonScope()
                .OnActivation((c, x) =>
                {
                    x.MessageRetentionPeriod = MessageRetentionPeriod;
                    x.MaxConcurrentMessages = MaxConcurrentReceivers;
                    x.AutoCreateQueueTable = AutoCreateQueues;
                    x.RequireHandler = true;
                    x.Endpoint = Endpoint;
                    x.ConnectionStrings = _connStrings;
                    x.SendOnly = SendOnly;
                    x.AutoStartProcessing = false;
                    x.UseReceiveTransactionForSending = ReuseReceiveConnectionForSending;
                    x.AllowUseOfApplicationDbConnectionForSending = UseAppManagedConnectionForSending;
                    x.ExposeReceiveConnection = ExposeReceiveConnectionToApplication;
                    x.DefaultTransactionTimeout = TransactionTimeout;
                    x.UseSqlOutputClause = _useSqlOutputClause;
                });
            _k.Bind<IMessageBus>().To<Impl.MessageBus>().InSingletonScope().Named("")
                .WithConstructorArgument(
                .OnActivation((c, x) =>
                {
                    x.BatchOutgoingMessagesInTransaction = BatchOutMessages;
                    x.UseTransactionScope = UseTransactionScope;
                    x.DefaultSubscriptionLifetime = SubscriptionLifetime;
                    x.PublishLocalByDefault = !SendOnly && AlwaysPublishLocal;
                    
                });
            //_wc.Kernel.ComponentRegistered += new Castle.MicroKernel.ComponentDataDelegate(Kernel_ComponentRegistered);
            //_wc.Kernel.ComponentUnregistered += new Castle.MicroKernel.ComponentDataDelegate(Kernel_ComponentUnregistered);

            _wc.Register(Component.For<IMessageBus>()
                .ImplementedBy<MessageBus.Impl.MessageBus>()
                .DependsOn(new
                {
                    BatchOutgoingMessagesInTransaction = BatchOutMessages,
                    UseTransactionScope = UseTransactionScope,
                    DefaultSubscriptionLifetime = SubscriptionLifetime,
                    PublishLocalByDefault = !SendOnly && AlwaysPublishLocal
                })
                .Parameters(Parameter.ForKey("transport").Eq("${MessageTransport_sql}"))
                .LifeStyle.Singleton);

            return this;

        }

        protected class AdditionalSqlBusConfig
        {
            public string BusName { get; set; }
            public string Endpoint { get; set; }
            /// <summary>
            /// max number of concurrently processed messages
            /// </summary>
            public int? MaxConcurrentMessages { get; set; }
            /// <summary>
            /// max messages received/second. Unlimited by default.
            /// </summary>
            public double? MaxReceiveFrequency { get; set; }
        }
        private List<AdditionalSqlBusConfig> _additionalBuses = new List<AdditionalSqlBusConfig>();

        public MessageBusConfigurator ConfigureAdditionalSqlMessageBus(string name, string endpoint)
        {
            _additionalBuses.Add(new AdditionalSqlBusConfig { BusName = name, Endpoint = endpoint });
            return this;
        }
        

        public MessageBusConfigurator Test()
        {
            IServiceResolver sr = _k.Get<IServiceResolver>();
            _k.Bind<IMessageBus>().To<NGinnBPM.MessageBus.Impl.MessageBus>()
                .InSingletonScope().Named("MessageBus")
                .OnActivation(delegate(NGinnBPM.MessageBus.Impl.MessageBus mb)
            {
               
            });
            return this;
        }

        protected bool IsServiceRegistered(Type t)
        {
            return _k.GetBindings(t).FirstOrDefault() != null;
        }
        protected bool IsServiceRegistered<T>() { return IsServiceRegistered(typeof(T)); }

        public static void RegisterHandlerType(Type t, IKernel k)
        {
            if (TypeUtil.IsSagaType(t))
            {
                //if (!IsServiceRegistered(wc, t)) RegisterSagaType(t, wc);
                throw new NotImplementedException();
                return;
            }

            List<Type> l = new List<Type>();
            l.Add(t);
            var l2 = TypeUtil.GetMessageHandlerInterfaces(t);
            var l3 = TypeUtil.GetMessageHandlerServiceInterfaces(t);
            if (l2.Count + l3.Count == 0) return;
            l.AddRange(l2);
            l.AddRange(l3);

            k.Bind(l.ToArray()).To(t).InTransientScope();
        }

        public void FinishConfiguration()
        {
            if (!IsServiceRegistered(typeof(IMessageBus)))
            {
                _k.Bind<IMessageBus>().To<Impl.MessageBus>().InSingletonScope()
                    .OnActivation((ctx, mb) =>
                    {
                        
                    });
            }
            if (!IsServiceRegistered(typeof(IMessageTransport)))
            {

            }
        }

        
    }
}
