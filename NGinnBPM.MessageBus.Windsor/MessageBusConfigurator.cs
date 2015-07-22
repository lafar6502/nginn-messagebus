using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Castle.Windsor;
using Castle.MicroKernel.Registration;
using NLog;
using NGinnBPM.MessageBus;
using NGinnBPM.MessageBus.Impl;
using NGinnBPM.MessageBus.Messages;
using System.Collections;
using System.Reflection;
using System.Data.Common;
using NGinnBPM.MessageBus.Impl.HttpService;
using NGinnBPM.MessageBus.Sagas;
using NGinnBPM.MessageBus.Impl.Sagas;
using NGinnBPM.MessageBus.Impl.SqlQueue;
using System.Configuration;
using System.IO;

namespace NGinnBPM.MessageBus.Windsor
{
    /// <summary>
    /// Message bus configuration helper 
    /// Provides functions for configuring NGinn MessageBus in a Castle Windsor container.
    /// </summary>
    public partial class MessageBusConfigurator 
    {
        private IWindsorContainer _wc;
        private List<ConnectionStringSettings> _connStrings = new List<ConnectionStringSettings>();
        private static Logger log = LogManager.GetCurrentClassLogger();
        
        private bool _useSqlOutputClause = false;

        public TimeSpan SubscriptionLifetime { get; set; }
        public bool BatchOutMessages { get;set;}
        public bool AutoCreateQueues { get; set; }
        public TimeSpan TransactionTimeout { get; set; }
        public bool AlwaysPublishLocal { get; set; }
        public bool EnableSagas { get; set; }
        public string DefaultDbProviderName { get;set;}
        
        private TimeSpan[] _retryTimes = new TimeSpan[] {
            TimeSpan.FromSeconds(30),
            TimeSpan.FromMinutes(3),
            TimeSpan.FromMinutes(20),
            TimeSpan.FromHours(2),
            TimeSpan.FromHours(8),
            TimeSpan.FromHours(24),
            TimeSpan.FromDays(3)
        };

        protected MessageBusConfigurator()
        {
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
            DefaultDbProviderName = "System.Data.SqlClient";
        }

        /// <summary>
        /// Begin configuration
        /// </summary>
        /// <returns></returns>
        public static MessageBusConfigurator Begin()
        {
            MessageBusConfigurator c = new MessageBusConfigurator();
            c.BeginConfig();
            return c;
        }

        /// <summary>
        /// Use an externally provided container for configuration
        /// </summary>
        /// <param name="container"></param>
        /// <returns></returns>
        public static MessageBusConfigurator Begin(IWindsorContainer container)
        {
            MessageBusConfigurator c = new MessageBusConfigurator();
            c._wc = container;
            c.BeginConfig();
            return c;
        }

        protected void BeginConfig()
        {
            if (_wc == null) _wc = new WindsorContainer();
            if (!IsServiceRegistered<IServiceResolver>())
            {
                _wc.Register(Component.For<IServiceResolver>().ImplementedBy<WindsorServiceResolver>().LifeStyle.Singleton);
            }
        }

        /// <summary>
        /// Add connection string alias
        /// </summary>
        /// <param name="alias"></param>
        /// <param name="connString"></param>
        /// <returns></returns>
        public  MessageBusConfigurator AddConnectionString(string alias, string connString, string providerName = null)
        {
            if (_connStrings.Any(x => x.Name == alias)) throw new Exception("Connection string already added: " + alias);
            _connStrings.Add(new ConnectionStringSettings {
                                 Name = alias,
                                 ConnectionString = connString,
                                 ProviderName = providerName ?? DefaultDbProviderName
                             });
            return this;
        }


        public IEnumerable<ConnectionStringSettings> GetConnectionStrings()
        {
            return _connStrings;
        }

        /// <summary>
        /// Supply an implementation of IServiceResolver that will be used for creating message handlers 
        /// and sagas. This way you can manage the lifetime of your message handlers in an external container
        /// and you don't have to register them in nginn-messagebus config.
        /// You have to implement the following method of your IServiceResolver:
        /// - HasService
        /// - GetAllInstances
        /// - GetInstance
        /// - ReleaseInstance
        /// </summary>
        /// <param name="externalResolver"></param>
        /// <returns></returns>
        public MessageBusConfigurator UseExternalHandlerContainer(IServiceResolver externalResolver)
        {
            _wc.Register(Component.For<IServiceResolver>().Instance(externalResolver).Named("ExternalServiceResolver"));
            
            return this;
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
        /// <param name="connStrings"></param>
        /// <returns></returns>
        public MessageBusConfigurator SetConnectionStrings(IEnumerable<ConnectionStringSettings> connStrings)
        {
            _connStrings  = new List<ConnectionStringSettings>(connStrings);
            return this;
        }

        /// <summary>
        /// Set a mapping: alias -> connection string
        /// </summary>
        /// <param name="connStrings"></param>
        /// <returns></returns>
        public MessageBusConfigurator SetConnectionStrings(IDictionary<string, string> connStrings)
        {
            SetConnectionStrings(connStrings.Select(kv => new ConnectionStringSettings { Name = kv.Key, ConnectionString = kv.Value, ProviderName = this.DefaultDbProviderName }));
            return this;
        }

        public MessageBusConfigurator SetDefaultDbProvider(string name)
        {
            this.DefaultDbProviderName = name;
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

        public string Endpoint { get;set;}
        

        [ThreadStatic]
        private static string _currentlyLoadedPlugin = null;

        public MessageBusConfigurator LoadPluginsFrom(string pluginDir)
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string dir = Path.IsPathRooted(pluginDir) ? pluginDir : Path.Combine(baseDir, pluginDir);
            var resolver = new ResolveEventHandler(CurrentDomain_AssemblyResolve);
            try
            {
                //AppDomain.CurrentDomain.AssemblyResolve += resolver;
                if (Directory.Exists(dir))
                {
                    foreach (string fn in Directory.GetFiles(dir, "*.dll"))
                    {
                        try
                        {
                            log.Info("Loading plugin: {0}", fn);
                            _currentlyLoadedPlugin = fn;
                            var asm = AppDomain.CurrentDomain.Load(Path.GetFileNameWithoutExtension(fn));
                            //Assembly asm = Assembly.Load(fn);
                            log.Info("Loaded {0}", fn);

                            bool pluginFound = false;
                            foreach (Type t in asm.GetTypes())
                            {
                                if (typeof(IPlugin).IsAssignableFrom(t))
                                {
                                    _wc.Register(Component.For<IPlugin>().ImplementedBy(t).LifeStyle.Singleton);
                                    pluginFound = true;
                                    break;
                                }
                            }
                            if (!pluginFound)
                            {
                                _wc.Install(Castle.Windsor.Installer.FromAssembly.Instance(asm));
                            }
                            log.Info("Finished loading of plugin: {0}", fn);
                        }
                        catch (Exception ex)
                        {
                            log.Error("Failed to load plugin {0}: {1}", fn, ex);
                        }
                    }

                    foreach (IPlugin pl in _wc.ResolveAll<IPlugin>())
                    {
                        pl.Register(_wc);
                    }
                }
                else
                {
                    log.Warn("Plugin directory does not exist: {0}", dir);
                }
            }
            finally
            {
                AppDomain.CurrentDomain.AssemblyResolve -= resolver;
                _currentlyLoadedPlugin = null;
            }

            
            return this;
        }

        Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            log.Info("Resolving assembly: " + args.Name);
            var s = _currentlyLoadedPlugin;

            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm.FullName == args.Name)
                {
                    log.Info("Resolved to currently loaded assembly {0}", asm.FullName);
                    return asm;
                }
            }
            string path = args.Name;
            if (File.Exists(path))
            {
                log.Info("Loading assembly from file {0}", path);
                byte[] data = System.IO.File.ReadAllBytes(path);
                return Assembly.Load(data);
            }
            else
            {
                throw new NotImplementedException("Assembly not found: " + path);
            }
        }

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

        private ConnectionStringSettings GetDefaultConnectionString()
        {
            if (Endpoint == null || Endpoint.Length == 0) throw new Exception("Configure endpoint first");
            string alias, table;
            if (!Impl.SqlUtil.ParseSqlEndpoint(Endpoint, out alias, out table))
                throw new Exception("Invalid endpoint");
            var cs = _connStrings.FirstOrDefault(x => x.Name == alias);
            if (cs == null) cs = SqlHelper.GetConnectionString(alias);
            if (cs == null)
                throw new Exception("Connection string not defined for alias: " + alias);
            return cs;
        }

        /// <summary>
        /// Configure SQL subscription database
        /// Warning: configure subscription parameters (lifetime, cache expiration time)
        /// before calling this function.
        /// </summary>
        /// <returns></returns>
        public MessageBusConfigurator UseSqlSubscriptions()
        {
            var connstr = GetDefaultConnectionString();
            _wc.Register(Component.For < ISubscriptionService, IMessageConsumer<Impl.InternalEvents.DatabaseInit>>()
                .ImplementedBy<NGinnBPM.MessageBus.Impl.SqlSubscriptionService>()
                .DependsOn(new
                {
                    ConnectionString = connstr.ConnectionString,
                    DbProvider = connstr.ProviderName,
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
            _wc.Register(Component.For<ISequenceMessages, IMessageConsumer<Impl.InternalEvents.DatabaseInit>>()
                .ImplementedBy<SqlSequenceManager>().LifeStyle.Singleton
                .DependsOn(new
                {
                    AutoCreateTable = true,
                    SequenceTable = "NGMB_SequenceInfo"
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

        

        /// <summary>
        /// Method of accessing currently used container
        /// during configuration
        /// </summary>
        /// <param name="action"></param>
        /// <returns></returns>
        public MessageBusConfigurator CustomizeContainer(Action<IWindsorContainer> action)
        {
            action(_wc);
            return this;
        }

        protected bool IsServiceRegistered<T>()
        {
            return IsServiceRegistered(typeof(T));
        }

        protected bool IsServiceRegistered(Type t)
        {
            return IsServiceRegistered(_wc, t);
        }

        public static bool IsServiceRegistered(IWindsorContainer wc, Type t)
        {
            return wc.Kernel.HasComponent(t);
        }

        protected MessageBusConfigurator ConfigureSqlMessageBus()
        {
            

            _wc.Register(Component.For<IMessageTransport, IStartableService, IHealthCheck, SqlMessageTransport2>()
                .ImplementedBy<SqlMessageTransport2>()
                .DependsOn(new
                {
                    MessageRetentionPeriod = MessageRetentionPeriod,
                    MaxConcurrentMessages = MaxConcurrentReceivers,
                    AutoCreateQueueTable = AutoCreateQueues,
                    RequireHandler = true,
                    Endpoint = Endpoint,
                    ConnectionStrings = _connStrings,
                    SendOnly = SendOnly,
                    AutoStartProcessing = false,
                    UseReceiveTransactionForSending = ReuseReceiveConnectionForSending,
                    AllowUseOfApplicationDbConnectionForSending = UseAppManagedConnectionForSending,
                    ExposeReceiveConnection = ExposeReceiveConnectionToApplication,
                    DefaultTransactionTimeout = TransactionTimeout,
                    UseSqlOutputClause = _useSqlOutputClause,
                    RetryTimes = _retryTimes
                })
                .Named("MessageTransport_sql")
                .LifeStyle.Singleton);

            


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
        
        /// <summary>
        /// Configures additional SQL message bus in same container, for receiving messages from
        /// other queues
        /// </summary>
        /// <param name="name"></param>
        /// <param name="endpoint"></param>
        /// <returns></returns>
        protected MessageBusConfigurator ConfigureAdditionalSqlMessageBus(AdditionalSqlBusConfig cfg)
        {
            string transName = "MessageTransport_sql_" + cfg.BusName;

            _wc.Register(Component.For<IMessageTransport, IStartableService, IHealthCheck, SqlMessageTransport2>()
                .ImplementedBy<SqlMessageTransport2>()
                .DependsOn(new
                {
                    MessageRetentionPeriod = MessageRetentionPeriod,
                    MaxConcurrentMessages = cfg.MaxConcurrentMessages.HasValue ? cfg.MaxConcurrentMessages.Value : MaxConcurrentReceivers,
                    AutoCreateQueueTable = AutoCreateQueues,
                    RequireHandler = true,
                    Endpoint = cfg.Endpoint,
                    ConnectionStrings = _connStrings,
                    SendOnly = false,
                    AutoStartProcessing = false,
                    UseReceiveTransactionForSending = ReuseReceiveConnectionForSending,
                    AllowUseOfApplicationDbConnectionForSending = UseAppManagedConnectionForSending,
                    ExposeReceiveConnection = ExposeReceiveConnectionToApplication,
                    DefaultTransactionTimeout = TransactionTimeout,
                    UseSqlOutputClause = _useSqlOutputClause,
                    RetryTimes = _retryTimes
                })
                .Named(transName)
                .LifeStyle.Singleton);

            if (!IsServiceRegistered<IMessageDispatcher>()) throw new Exception("no message dispatcher");

            _wc.Register(Component.For<IMessageBus>()
                .ImplementedBy<MessageBus.Impl.MessageBus>()
                .DependsOn(new
                {
                    BatchOutgoingMessagesInTransaction = BatchOutMessages,
                    UseTransactionScope = UseTransactionScope,
                    DefaultSubscriptionLifetime = SubscriptionLifetime,
                    PublishLocalByDefault = AlwaysPublishLocal
                })
                .Parameters(Parameter.ForKey("transport").Eq("${" + transName + "}"))
                .Named(cfg.BusName)
                .LifeStyle.Singleton);
            return this;
        }

        private void MessageConsumerAddedOrRemoved()
        {
            if (!IsServiceRegistered<MessageDispatcher>()) return;
            MessageDispatcher md = _wc.Resolve<MessageDispatcher>();
            if (md != null)
            {
                md.HandlerConfigurationChanged();
            }
        }


        /// <summary>
        /// Configure message retention period
        /// </summary>
        /// <param name="ts"></param>
        /// <returns></returns>
        public MessageBusConfigurator SetMessageRetentionPeriod(TimeSpan ts)
        {
            MessageRetentionPeriod = ts;
            return this;
        }

        public TimeSpan MessageRetentionPeriod { get; set; }

        /// <summary>
        /// Configure default subscription expiration time
        /// If this time is set to zero subscriptions will never expire
        /// </summary>
        /// <param name="ts"></param>
        /// <returns></returns>
        public MessageBusConfigurator SetDefaultSubscriptionLifetime(TimeSpan ts)
        {
            SubscriptionLifetime = ts;
            return this;
        }

        private TimeSpan _subscriptionCacheTime = TimeSpan.FromMinutes(60);

        /// <summary>
        /// Configure subscription cache expiration time.
        /// By default it's 1 hour.
        /// </summary>
        /// <param name="ts"></param>
        /// <returns></returns>
        public MessageBusConfigurator SetSubscriptionCacheTime(TimeSpan ts)
        {
            _subscriptionCacheTime = ts;
            return this;
        }
        /// <summary>
        /// Set number of message processing threads 
        /// </summary>
        /// <param name="m"></param>
        /// <returns></returns>
        public MessageBusConfigurator SetMaxConcurrentMessages(int m)
        {
            MaxConcurrentReceivers = m;
            return this;
        }

        public int MaxConcurrentReceivers { get; set; }

        /// <summary>
        /// Add message handler information
        /// If the handler requires any custom configuration, use the CustomizeContainer method
        /// to register it in a windsor container.
        /// </summary>
        /// <param name="handlerType"></param>
        /// <returns></returns>
        public MessageBusConfigurator AddMessageHandler(Type handlerType)
        {
            RegisterHandlerType(handlerType, _wc);
            return this;
        }

        /// <summary>
        /// Register saga type.
        /// All message consumer and all service handler interfaces are registered so
        /// you should not register this type again as a message handler and a http service handler.
        /// </summary>
        /// <param name="sagaType"></param>
        /// <returns></returns>
        protected static void RegisterSagaType(Type sagaType, IWindsorContainer wc)
        {
            if (!TypeUtil.IsSagaType(sagaType))
            {
                throw new Exception("Is not a saga");
            }

            if (IsServiceRegistered(wc, sagaType))
            {
                throw new Exception("Saga type already registered: " + sagaType);
            }
            log.Info("Registering saga: {0}", sagaType);
            var l = new List<Type>();
            l.Add(sagaType);
            l.Add(typeof(SagaBase));
            l.AddRange(TypeUtil.GetMessageHandlerInterfaces(sagaType));
            l.AddRange(TypeUtil.GetMessageHandlerServiceInterfaces(sagaType));
            wc.Register(Component.For(l).ImplementedBy(sagaType).LifeStyle.Transient);
        }

        public MessageBusConfigurator RegisterSagaType(Type sagaType)
        {
            RegisterSagaType(sagaType, _wc);
            return this;
        }

        /// <summary>
        /// Add an instance of a message handler component
        /// </summary>
        /// <param name="instance"></param>
        /// <returns></returns>
        public MessageBusConfigurator AddMessageHandlerInstance(object instance)
        {
            IList<Type> l = TypeUtil.GetMessageHandlerInterfaces(instance.GetType());
            if (l.Count > 0)
            {
                _wc.Register(Component.For(l).Instance(instance));
            }
            return this;
        }

        /// <summary>
        /// Add all message handlers that are found in specified assembly.
        /// </summary>
        /// <param name="asm"></param>
        /// <returns></returns>
        public MessageBusConfigurator AddMessageHandlersFromAssembly(System.Reflection.Assembly asm)
        {
            RegisterMessageHandlersFromAssembly(asm, _wc);
            return this;
        }

        public static void RegisterHandlerType(Type t, IWindsorContainer wc)
        {
            RegisterHandlerType(t, wc, null, null);
        }

        public static void RegisterHandlerType(Type t, IWindsorContainer wc, bool transient)
        {
            RegisterHandlerType(t, wc, transient, null);
        }

        public static void RegisterHandlerType(Type t, IWindsorContainer wc, bool? transient, object depends)
        {
            if (TypeUtil.IsSagaType(t))
            {
                if (!IsServiceRegistered(wc, t)) RegisterSagaType(t, wc);
                return;
            }

            List<Type> l = new List<Type>();
            l.Add(t);
            var l2 = TypeUtil.GetMessageHandlerInterfaces(t);
            var l3 = TypeUtil.GetMessageHandlerServiceInterfaces(t);
            if (l2.Count + l3.Count == 0) return; 
            l.AddRange(l2);
            l.AddRange(l3);
            
            var reg = Component.For(l).ImplementedBy(t);
            if (transient.HasValue)
            {
                reg = transient.Value ? reg.LifeStyle.Transient : reg.LifeStyle.Singleton;
            }
            else
            {
                MessageHandlerConfigAttribute attr = (MessageHandlerConfigAttribute)Attribute.GetCustomAttribute(t, typeof(MessageHandlerConfigAttribute));
                if (attr != null) reg = attr.Transient ? reg.LifeStyle.Transient : reg.LifeStyle.Singleton;
            }
            if (depends != null) reg = reg.DependsOn(depends);
            wc.Register(reg);
        }

        
        public static void RegisterMessageHandlersFromAssembly(Assembly asm, IWindsorContainer wc)
        {
            foreach (Type t in asm.GetTypes())
            {
                if (!IsServiceRegistered(wc, t))
                {
                    RegisterHandlerType(t, wc);
                }
            }
        }

        /// <summary>
        /// Configure the message bus as send-only
        /// In this configuration you will be able only to send messages to a remote database
        /// without using any local message store. No messages will be received.
        /// Info: if you want to store&forward outgoing messages in a local database,
        /// don't use send-only mode.
        /// </summary>
        /// <returns></returns>
        public MessageBusConfigurator SetSendOnly(bool sendOnly)
        {
            SendOnly = sendOnly;
            return this;
        }

        public bool SendOnly { get; set; }

        /// <summary>
        /// Retrieve message bus interface
        /// </summary>
        /// <returns></returns>
        public IMessageBus GetMessageBus()
        {
            return _wc.Resolve<IMessageBus>();
        }

        public bool AutoStart { get; set; }
        /// <summary>
        /// Set this to true to auto start the message bus
        /// after BuildContainer is called
        /// </summary>
        /// <param name="autoStart"></param>
        /// <returns></returns>
        public MessageBusConfigurator AutoStartMessageBus(bool autoStart)
        {
            AutoStart = autoStart;
            return this;
        }

        /// <summary>
        /// Call this to start the message bus (after it has been set up)
        /// </summary>
        /// <returns></returns>
        public MessageBusConfigurator StartMessageBus()
        {
            IMessageBus bus = _wc.Resolve<IMessageBus>();
            if (bus == null) throw new Exception("Message bus not configured");
            foreach (IStartableService iss in _wc.ResolveAll<IStartableService>())
            {
                log.Info("Starting {0}", iss);
                iss.Start();
                log.Info("Started {0}", iss);
            }
            return this;
        }

        /// <summary>
        /// Stop the message bus 
        /// </summary>
        public MessageBusConfigurator StopMessageBus()
        {
            StopMessageBus(_wc);
            return this;
        }

        /// <summary>
        /// Stop the message bus 
        /// </summary>
        /// <param name="wc"></param>
        public static void StopMessageBus(IWindsorContainer wc)
        {
            foreach (IStartableService iss in wc.ResolveAll<IStartableService>())
            {
                log.Info("Stopping {0}", iss);
                iss.Stop();
                log.Info("Stopped {0}", iss);
            }
        }

        /// <summary>
        /// Configure HTTP message receiver at specified address
        /// </summary>
        /// <param name="listenAddress">http://[hostname or IP]:[port number], for example http://localhost:9090 or http://+:9090 for all IP addresses</param>
        /// <returns></returns>
        public MessageBusConfigurator ConfigureHttpReceiver(string listenAddress)
        {
            string endpoint = listenAddress.Replace("+", Environment.MachineName);

            _wc.Register(Component.For<IStartableService>().ImplementedBy<NGinnBPM.MessageBus.Impl.HttpService.HttpServer>()
                .DependsOn(new { ListenAddress = listenAddress }).LifeStyle.Singleton);
            if (!IsServiceRegistered<MasterDispatcherServlet>())
            {
                _wc.Register(Component.For<MasterDispatcherServlet>().ImplementedBy<MasterDispatcherServlet>().LifeStyle.Singleton);
            }
            _wc.Register(Component.For<HttpMessageTransport, IMessageTransport>()
                .ImplementedBy<HttpMessageTransport>()
                .LifeStyle.Singleton
                .Named("MessageTransport_http")
                .Parameters(Parameter.ForKey("Endpoint").Eq(endpoint)));
            log.Info("Registered http message transport for endpoint {0}", endpoint);
            log.Info("Http listener configured for {0}", listenAddress);
            _wc.Register(Component.For<IReceivedMessageRegistry>()
                .ImplementedBy<SqlReceivedMessageRegistry>()
                .LifeStyle.Singleton
                .DependsOn(new
                {
                }));
            _wc.Register(Component.For<HttpMessageGateway>()
                .ImplementedBy<HttpMessageGateway>()
                .Parameters(Parameter.ForKey("busTransport").Eq("${MessageTransport_sql}"), Parameter.ForKey("httpTransport").Eq("${MessageTransport_http}"))
                .LifeStyle.Singleton);

            RegisterHttpHandlersFromAssembly(typeof(NGinnBPM.MessageBus.IMessageBus).Assembly);
            _wc.Register(Component.For<IServlet>()
                .ImplementedBy<StaticResourceServlet>()
                .LifeStyle.Singleton
                .DependsOn(new
                {
                    MatchUrl = @"(^/index.htm$|^/rc/(?<id>.+)?)",
                    SourceAssembly = typeof(IMessageBus).Assembly,
                    ResourcePrefix = "NGinnBPM.MessageBus.StaticRC"
                }));
            return this;
        }

        public MessageBusConfigurator RegisterHttpHandler(Type t)
        {
            NGinnBPM.MessageBus.Impl.HttpService.UrlPatternAttribute at = (NGinnBPM.MessageBus.Impl.HttpService.UrlPatternAttribute) Attribute.GetCustomAttribute(t, typeof(NGinnBPM.MessageBus.Impl.HttpService.UrlPatternAttribute));
            if (at != null)
            {
                ComponentRegistration<NGinnBPM.MessageBus.Impl.HttpService.IServlet> reg = Component.For<NGinnBPM.MessageBus.Impl.HttpService.IServlet>().ImplementedBy(t)
                    .DependsOn(new { MatchUrl = at.Pattern }).LifeStyle.Transient;
                _wc.Register(reg);
                log.Info("URL: {0}, handler: {1}", at.Pattern, t.FullName);
            }
            else
            {
                log.Info("Url pattern not specified for http servlet {0}", t.FullName);
            }
            return this;
        }

        /// <summary>
        /// </summary>
        /// <param name="asm"></param>
        /// <returns></returns>
        public MessageBusConfigurator RegisterHttpHandlersFromAssembly(Assembly asm)
        {
            foreach (Type t in asm.GetTypes())
            {
                if (t.IsAbstract) continue;
                if (typeof(NGinnBPM.MessageBus.Impl.HttpService.IServlet).IsAssignableFrom(t))
                {
                    RegisterHttpHandler(t);
                }
            }
            return this;
        }

        /// <summary>
        /// Registers all types that implement the IMessageHandlerService interface.
        /// If the registered type is a saga all of its message handling interfaces will be 
        /// registered as well. Sagas are registered as transient and 'normal' services
        /// are singletons by default.
        /// </summary>
        /// <param name="asm"></param>
        /// <returns></returns>
        public MessageBusConfigurator RegisterHttpMessageServicesFromAssembly(Assembly asm)
        {
            foreach (Type t in asm.GetTypes())
            {
                if (typeof(IMessageHandlerServiceBase).IsAssignableFrom(t) && !IsServiceRegistered(_wc, t))
                {
                    RegisterHttpMessageService(t);
                }
            }
            return this;
        }

        public MessageBusConfigurator RegisterHttpMessageService(Type t)
        {
            if (TypeUtil.IsSagaType(t))
            {
                if (!IsServiceRegistered(t)) RegisterSagaType(t);
                return this;
            }

            var l = TypeUtil.GetMessageHandlerServiceInterfaces(t);
            var l2 = TypeUtil.GetMessageHandlerInterfaces(t);
            if (l.Count > 0)
            {
                _wc.Register(Component.For(l.ToArray()).ImplementedBy(t).LifeStyle.Singleton);
            }
            return this;
        }

        public MessageBusConfigurator SetRetryTimes(TimeSpan[] retries)
        {
            _retryTimes = retries;
            return this;
        }


        public IWindsorContainer Container
        {
            get { return _wc; }
        }

        public IServiceResolver ServiceResolver
        {
            get { return _wc.Resolve<IServiceResolver>(); }
        }

        /// <summary>
        /// Final configuration method, configures the 
        /// message bus according to all previously specified
        /// config options.
        /// </summary>
        /// <returns></returns>
        public MessageBusConfigurator FinishConfiguration()
        {
            if (!IsServiceRegistered<IMessageDispatcher>())
            {
                var c = _wc.Kernel.ConfigurationStore.GetComponentConfiguration("ExternalServiceResolver");
                var reg = Component.For<IMessageDispatcher, MessageDispatcher>()
                    .ImplementedBy<MessageDispatcher>()
                    .LifeStyle.Singleton;
                if (c != null)
                {
                    reg = reg.Parameters(Parameter.ForKey("resolver").Eq("${ExternalServiceResolver}"));
                }
                _wc.Register(reg);
            }

            if (!IsServiceRegistered<IServiceMessageDispatcher>())
            {
                _wc.Register(Component.For<IServiceMessageDispatcher>()
                    .ImplementedBy<ServiceMessageDispatcher>().LifeStyle.Singleton);
            }
            _wc.Register(Component.For<JsonServiceCallHandler>().ImplementedBy<JsonServiceCallHandler>());
            if (!IsServiceRegistered<IMessageConsumer<Ping>>())
            {
                RegisterHandlerType(typeof(PingService), _wc);
            }
            if (!IsServiceRegistered<IMessageConsumer<SubscribeRequest>>())
            {
                _wc.Register(Component.For<IMessageConsumer<SubscribeRequest>, IMessageConsumer<UnsubscribeRequest>, IMessageConsumer<SubscriptionExpiring>, IMessageConsumer<SubscriptionTimeout>>()
                    .ImplementedBy<SubscriptionMsgHandler>()
                    .DependsOn(new
                    {
                        DefaultSubscriptionLifetime = SubscriptionLifetime
                    }));
            }
            if (!IsServiceRegistered<ISerializeMessages>())
            {
                _wc.Register(Component.For<ISerializeMessages>().ImplementedBy<JsonMessageSerializer>().LifeStyle.Singleton);
            }
            if (!IsServiceRegistered<ISubscriptionService>())
            {
                UseSqlSubscriptions();
            }
            var dcs = GetDefaultConnectionString();
            if (EnableSagas)
            {
                if (!IsServiceRegistered<SagaStateHelper>())
                {
                    _wc.Register(Component.For<SagaStateHelper>().ImplementedBy<SagaStateHelper>().LifeStyle.Singleton);
                }
                if (!IsServiceRegistered<ISagaRepository>())
                {
                    string calias, tmp;
                    ConnectionStringSettings cs = null;
                    if (SqlUtil.ParseSqlEndpoint(Endpoint, out calias, out tmp))
                    {
                        cs = _connStrings.FirstOrDefault(x => x.Name == calias);
                        if (cs == null) cs = SqlHelper.GetConnectionString(calias);
                    } else throw new Exception("Endpoint");
                    _wc.Register(Component.For<ISagaRepository>().ImplementedBy<SqlSagaStateRepository>().LifeStyle.Singleton
                        .DependsOn(new
                        {
                            ConnectionString = cs == null ? calias : cs.ConnectionString,
                            ProviderName = cs == null ? DefaultDbProviderName : cs.ProviderName,
                            TableName = "NG_Sagas",
                            AutoCreateDatabase = AutoCreateQueues
                        }));
                }
                
            }
            if (!IsServiceRegistered<IMessageBus>())
            {
                ConfigureSqlMessageBus();
            }
            var dmb = _wc.Resolve<IMessageBus>();
            foreach (var ac in _additionalBuses)
            {
                log.Info("Configuring additional message bus {0} | {1}", ac.Endpoint, ac.BusName);
                ConfigureAdditionalSqlMessageBus(ac);
            }
            foreach (IPlugin pl in _wc.ResolveAll<IPlugin>())
            {
                pl.OnFinishConfiguration(_wc);
            }
            if (dcs != null)
            {
                using (var con = SqlHelper.OpenConnection(dcs))
                {
                    IMessageDispatcher md = _wc.Resolve<IMessageDispatcher>();
                    md.DispatchMessage(new Impl.InternalEvents.DatabaseInit { Connection = con }, dmb);
                }
            }
            if (AutoStart)
            {
                foreach (IMessageBus mb in _wc.ResolveAll<IMessageBus>())
                {
                    log.Info("Message bus configured: {0}", mb.Endpoint);
                }
                StartMessageBus();
            }
            if (IsServiceRegistered<HttpMessageGateway>()) _wc.Resolve<HttpMessageGateway>();
            return this;
        }

        private string GetAppConfigString(string key, string defval)
        {
            var s = ConfigurationManager.AppSettings[key];
            if (s == null) s = defval;
            if (s == null) return defval;
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            s = s.Replace("${basedir}", baseDir);
            s = s.Replace("${machineName}", Environment.MachineName);
            return s;
        }

        /// <summary>
        /// Create a queue table in SQL server database.
        /// Currently this will fail for other db types.
        /// Be sure to configure your connection strings before calling this function.
        /// </summary>
        /// <param name="endpoint"></param>
        /// <returns></returns>
        public MessageBusConfigurator CreateQueueTable(string endpoint)
        {
            var dcs = GetDefaultConnectionString();
            string cs, table;
            if (!SqlUtil.ParseSqlEndpoint(endpoint, out cs, out table)) throw new Exception("Invalid sql endpoint");
            using (var con = SqlHelper.OpenConnection(cs))
            {
                SqlHelper.RunDDLFromResource(con, "NGinnBPM.MessageBus.createmqueue.${dialect}.sql", new object[] { table });
            }
            return this;
        }

        protected DbConnection OpenConnection(string cstring)
        {
            var cs = _connStrings.FirstOrDefault(x => x.Name == cstring);
            return SqlHelper.OpenConnection(cs == null ? cstring : cs.ConnectionString, cs == null ? null : cs.ProviderName);
        }
        /// <summary>
        /// Read the configuration from app config file
        /// </summary>
        /// <returns></returns>
        public MessageBusConfigurator ConfigureFromAppConfig()
        {
            foreach (ConnectionStringSettings cs in ConfigurationManager.ConnectionStrings)
            {
                AddConnectionString(cs.Name, cs.ConnectionString);
            }
            SetEndpoint(GetAppConfigString("NGinnMessageBus.Endpoint", null));
            SetMaxConcurrentMessages(Int32.Parse(GetAppConfigString("NGinnMessageBus.MaxConcurrentMessages", "4")));
            _useSqlOutputClause = bool.Parse(GetAppConfigString("NGinnMessageBus.UseSqlOutputClause", "false"));
            string rf = GetAppConfigString("NGinnMessageBus.RoutingFile", null);
            if (!string.IsNullOrEmpty(rf)) UseStaticMessageRouting(rf);
            string s = GetAppConfigString("NGinnMessageBus.HttpListener", null);
            if (!string.IsNullOrEmpty(s)) ConfigureHttpReceiver(s);
            s = GetAppConfigString("NGinnMessageBus.MessageRetentionPeriod", null);
            if (!string.IsNullOrEmpty(s)) SetMessageRetentionPeriod(TimeSpan.Parse(s));
            this.SetExposeReceiveConnectionToApplication(true);
            this.UseApplicationManagedConnectionForSending(true);
            SetEnableSagas(bool.Parse(GetAppConfigString("NGinnMessageBus.EnableSagas", "true")));
            SetSendOnly(bool.Parse(GetAppConfigString("NGinnMessageBus.SendOnly", "false")));
            AutoCreateDatabase(bool.Parse(GetAppConfigString("NGinnMessageBus.AutoCreateDatabase", "true")));
            SetAlwaysPublishLocal(bool.Parse(GetAppConfigString("NGinnMessageBus.AlwaysPublishLocal", "true")));
            s = GetAppConfigString("NGinnMessageBus.PluginDirectory", null);
            if (!string.IsNullOrEmpty(s))
            {
                LoadPluginsFrom(s);
            }
            return this;
        }

        
    }
}
