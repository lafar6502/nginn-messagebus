using Microsoft.Extensions.DependencyInjection;
using NGinnBPM.MessageBus;
using NGinnBPM.MessageBus.Impl;
using System.Reflection;
using System.Configuration;
using NLog;
using NGinnBPM.MessageBus.Impl.SqlQueue;
using NGinnBPM.MessageBus.Impl.HttpService;
using NGinnBPM.MessageBus.Messages;
using NGinnBPM.MessageBus.Impl.Sagas;

namespace NGinnBPM.MessageBus.MSDependencyInjection
{
    public class MessageBusConfigBuilder
    {
        protected IServiceCollection _container;

        private List<ConnectionStringInfo> _connStrings = new List<ConnectionStringInfo>();
        private static Logger log = LogManager.GetCurrentClassLogger();

        private bool _useSqlOutputClause = false;

        public TimeSpan SubscriptionLifetime { get; set; }
        public bool BatchOutMessages { get; set; }
        public bool AutoCreateQueues { get; set; }
        public TimeSpan TransactionTimeout { get; set; }
        public bool AlwaysPublishLocal { get; set; }
        public bool EnableSagas { get; set; }
        public bool SendOnly { get; set; }
        public string DefaultDbProviderName { get; set; }

        private TimeSpan[] _retryTimes = new TimeSpan[] {
            TimeSpan.FromSeconds(30),
            TimeSpan.FromMinutes(3),
            TimeSpan.FromMinutes(20),
            TimeSpan.FromHours(2),
            TimeSpan.FromHours(8),
            TimeSpan.FromHours(24),
            TimeSpan.FromDays(3)
        };

        public static MessageBusConfigBuilder Begin(IServiceCollection col)
        {
            return new MessageBusConfigBuilder
            {
                _container = col
            };
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
        public MessageBusConfigBuilder UseExternalHandlerContainer(IServiceResolver externalResolver)
        {
            _container.AddKeyedSingleton(typeof(IServiceResolver), "ExternalServiceResolver", externalResolver);
            return this;
        }

        /// <summary>
        /// Add connection string alias
        /// </summary>
        /// <param name="alias"></param>
        /// <param name="connString"></param>
        /// <returns></returns>
        public MessageBusConfigBuilder AddConnectionString(string alias, string connString, string providerName = null)
        {
            if (_connStrings.Any(x => x.Alias == alias)) throw new Exception("Connection string already added: " + alias);
            _connStrings.Add(new ConnectionStringInfo
            {
                Alias = alias,
                ConnectionString = connString,
                ProviderName = providerName ?? DefaultDbProviderName
            });
            return this;
        }


        public IEnumerable<ConnectionStringInfo> GetConnectionStrings()
        {
            return _connStrings;
        }

        /// <summary>
        /// Enable/disable batch sending of all outgoing messages in a transaction
        /// </summary>
        /// <param name="b"></param>
        /// <returns></returns>
        public MessageBusConfigBuilder BatchOutgoingMessages(bool b)
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
        public MessageBusConfigBuilder SetReceiveTransactionTimeout(TimeSpan ts)
        {
            TransactionTimeout = ts;
            return this;
        }

        /// <summary>
        /// Set all connection string aliases at once
        /// </summary>
        /// <param name="connStrings"></param>
        /// <returns></returns>
        public MessageBusConfigBuilder SetConnectionStrings(IEnumerable<ConnectionStringInfo> connStrings)
        {
            _connStrings = new List<ConnectionStringInfo>(connStrings);
            return this;
        }

        /// <summary>
        /// Set a mapping: alias -> connection string
        /// </summary>
        /// <param name="connStrings"></param>
        /// <returns></returns>
        public MessageBusConfigBuilder SetConnectionStrings(IDictionary<string, string> connStrings)
        {
            SetConnectionStrings(connStrings.Select(kv => new ConnectionStringInfo { Alias = kv.Key, ConnectionString = kv.Value, ProviderName = this.DefaultDbProviderName }));
            return this;
        }

        public MessageBusConfigBuilder SetDefaultDbProvider(string name)
        {
            this.DefaultDbProviderName = name;
            return this;
        }

        /// <summary>
        /// Set message bus endpoint name
        /// </summary>
        /// <param name="endpoint"></param>
        /// <returns></returns>
        public MessageBusConfigBuilder SetEndpoint(string endpoint)
        {
            Endpoint = endpoint;
            return this;
        }

        public string Endpoint { get; set; }

        private ConnectionStringInfo GetDefaultConnectionString()
        {
            if (Endpoint == null || Endpoint.Length == 0) throw new Exception("Configure endpoint first");
            string alias, table;
            if (!Impl.SqlUtil.ParseSqlEndpoint(Endpoint, out alias, out table))
                throw new Exception("Invalid endpoint");
            var cs = _connStrings.FirstOrDefault(x => x.Alias == alias);
            if (cs == null) cs = SqlHelper.GetConnectionString(alias);
            if (cs == null)
                throw new Exception("Connection string not defined for alias: " + alias);
            return cs;
        }

        public MessageBusConfigBuilder UseSqlSubscriptions()
        {
            var connstr = GetDefaultConnectionString();
            DIHelper.RegisterService<NGinnBPM.MessageBus.Impl.SqlSubscriptionService>(new Type[] { typeof(ISubscriptionService), typeof(IMessageConsumer<Impl.InternalEvents.DatabaseInit>) },
                _container, ServiceLifetime.Singleton, sp =>
                {
                    return new NGinnBPM.MessageBus.Impl.SqlSubscriptionService
                    {
                        AutoCreateSubscriptionTable = true,
                        ConnectionString = connstr.ConnectionString,
                        DbProvider = connstr.ProviderName,
                        CacheExpiration = TimeSpan.FromDays(1),
                        Endpoint = Endpoint
                    };
                });
                
            return this;
        }

        public MessageBusConfigBuilder UseStaticMessageRouting(string configFile)
        {
            DIHelper.RegisterService<StaticMessageRouting>(new Type[] { typeof(ISubscriptionService) }, _container, ServiceLifetime.Singleton, sp =>
            {
                return new StaticMessageRouting
                {
                    ConfigFile = configFile
                };
            });
            return this;
        }

        public MessageBusConfigBuilder AddMessageHandlersFromAssembly(System.Reflection.Assembly asm)
        {
            DIHelper.RegisterMessageHandlersFromAssembly(asm, _container);
            return this;
        }

        /// <summary>
        /// Configure message bus to use message sequence repository
        /// stored in default database in NGinnMessageBus_Sequences table
        /// </summary>
        /// <returns></returns>
        public MessageBusConfigBuilder UseSqlSequenceManager()
        {
            DIHelper.RegisterService<SqlSequenceManager, ISequenceMessages, IMessageConsumer<Impl.InternalEvents.DatabaseInit>>(_container, ServiceLifetime.Singleton, sp =>
            {
                return new SqlSequenceManager
                {
                    AutoCreateTable = true,
                    SequenceTable = "NGMB_SequenceInfo"
                };
            });
            return this;
        }

        public MessageBusConfigBuilder SetEnableSagas(bool enable)
        {
            EnableSagas = enable;
            return this;
        }

        /// <summary>
        /// Configure the message bus as send-only
        /// In this configuration you will be able only to send messages to a remote database
        /// without using any local message store. No messages will be received.
        /// Info: if you want to store&forward outgoing messages in a local database,
        /// don't use send-only mode.
        /// </summary>
        /// <returns></returns>
        public MessageBusConfigBuilder SetSendOnly(bool sendOnly)
        {
            SendOnly = sendOnly;
            return this;
        }

        /// <summary>
        /// Set number of message processing threads 
        /// </summary>
        /// <param name="m"></param>
        /// <returns></returns>
        public MessageBusConfigBuilder SetMaxConcurrentMessages(int m)
        {
            MaxConcurrentReceivers = m;
            return this;
        }

        public int MaxConcurrentReceivers { get; set; } = 4;

        /// <summary>
        /// Configure message retention period
        /// </summary>
        /// <param name="ts"></param>
        /// <returns></returns>
        public MessageBusConfigBuilder SetMessageRetentionPeriod(TimeSpan ts)
        {
            MessageRetentionPeriod = ts;
            return this;
        }

        public TimeSpan MessageRetentionPeriod { get; set; } = TimeSpan.FromDays(10);

        /// <summary>
        /// Set this to false to disable enclosing of message handler in System.Transactions.TransactionScope.
        /// Without transaction scope you get better performance but no global transaction handling.
        /// Transaction scope is enabled by default. Disable it only if necessary.
        /// </summary>
        /// <param name="use"></param>
        /// <returns></returns>
        public MessageBusConfigBuilder SetUseTransactionScope(bool use)
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
        public MessageBusConfigBuilder SetReuseReceiveConnectionForSending(bool b)
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
        public MessageBusConfigBuilder SetExposeReceiveConnectionToApplication(bool b)
        {
            ExposeReceiveConnectionToApplication = b;
            return this;
        }

        public bool UseAppManagedConnectionForSending { get; set; } = true;
        public bool RequireAppManagedConnectionForSending { get; set; } = false;

        protected MessageBusConfigBuilder ConfigureSqlMessageBus()
        {
            DIHelper.RegisterService<SqlMessageTransport2, IMessageTransport, IStartableService, IHealthCheck>(_container, ServiceLifetime.Singleton, sp =>
            {
                var t = new SqlMessageTransport2(sp.GetService<ITransactionScopeFactory>(), sp.GetService<ISequenceMessages>())
                {
                    MessageRetentionPeriod = MessageRetentionPeriod,
                    MaxConcurrentMessages = MaxConcurrentReceivers,
                    AutoCreateQueueTable = AutoCreateQueues,
                    AddDelayMsAfterWakeup = 0,
                    Endpoint = Endpoint,
                    ConnectionStrings = _connStrings,
                    SendOnly = SendOnly,
                    UseReceiveTransactionForSending = ReuseReceiveConnectionForSending,
                    AllowUseOfApplicationDbConnectionForSending = UseAppManagedConnectionForSending,
                    RequireUseOfApplicationDbConnectionForSending = UseAppManagedConnectionForSending && RequireAppManagedConnectionForSending,
                    ExposeReceiveConnection = ExposeReceiveConnectionToApplication,
                    UseSqlOutputClause = _useSqlOutputClause,
                    RetryTimes = _retryTimes,
                };
                return t;
            });

            DIHelper.RegisterService<MessageBus.Impl.MessageBus, IMessageBus>(_container, ServiceLifetime.Singleton, sp =>
            {
                return new Impl.MessageBus(sp.GetService<IMessageTransport>(), sp.GetService<IMessageDispatcher>(), sp.GetService<ISerializeMessages>(),
                    sp.GetService<IServiceResolver>(), sp.GetService<ITransactionScopeFactory>())
                {
                    BatchOutgoingMessagesInTransaction = BatchOutMessages,
                    UseTransactionScope = UseTransactionScope,
                    DefaultSubscriptionLifetime = SubscriptionLifetime,
                    PublishLocalByDefault = !SendOnly && AlwaysPublishLocal
                };
            });

            return this;

        }

        /// <summary>
        /// Final configuration method, configures the 
        /// message bus according to all previously specified
        /// config options.
        /// </summary>
        /// <returns></returns>
        public MessageBusConfigBuilder FinishConfiguration()
        {
            if (!DIHelper.IsServiceRegistered<IServiceResolver>(_container))
            {
                _container.AddSingleton<IServiceResolver, NetDIServiceResolver>();
            }
            if (!DIHelper.IsServiceRegistered<IMessageDispatcher>(_container))
            {
                var hasExternal = _container.Any(x => x.IsKeyedService && "ExternalServiceResolver".Equals(x.ServiceKey) && x.ServiceType.IsAssignableTo(typeof(IServiceResolver)));
                DIHelper.RegisterService<MessageDispatcher, IMessageDispatcher>(_container, ServiceLifetime.Singleton, sp =>
                {
                    var r = new MessageDispatcher(hasExternal ? sp.GetKeyedService<IServiceResolver>("ExternalServiceResolver") : sp.GetService<IServiceResolver>());
                    return r;
                });
            }
            if (!DIHelper.IsServiceRegistered<ITransactionScopeFactory>(_container))
            {
                DIHelper.RegisterService<TransactionScopeFactoryEx, ITransactionScopeFactory>(_container, ServiceLifetime.Singleton, sp =>
                {
                    return new TransactionScopeFactoryEx
                    {
                        DefaultTransactionTimeout = TransactionTimeout
                    };
                });
            }
            if (!DIHelper.IsServiceRegistered<IServiceMessageDispatcher>(_container))
            {
                DIHelper.RegisterService<ServiceMessageDispatcher, IServiceMessageDispatcher>(_container, ServiceLifetime.Singleton);
            }
            _container.AddSingleton<JsonServiceCallHandler>();

            if (!DIHelper.IsServiceRegistered<IMessageConsumer<Ping>>(_container))
            {
                DIHelper.RegisterHandlerType(typeof(PingService), _container, false);
            }
            if (!DIHelper.IsServiceRegistered<IMessageConsumer<SubscribeRequest>>(_container))
            {
                DIHelper.RegisterService<SubscriptionMsgHandler, IMessageConsumer<SubscribeRequest>, IMessageConsumer<UnsubscribeRequest>, IMessageConsumer<SubscriptionExpiring>, IMessageConsumer<SubscriptionTimeout>>(
                    _container, ServiceLifetime.Singleton, sp =>
                    {
                        return new SubscriptionMsgHandler(sp.GetService<ISubscriptionService>(), sp.GetService<IMessageBus>())
                        {
                            DefaultSubscriptionLifetime = SubscriptionLifetime,
                        };
                    });
            }
            if (!DIHelper.IsServiceRegistered<ISerializeMessages>(_container))
            {
                DIHelper.RegisterService<JsonMessageSerializer, ISerializeMessages>(_container, ServiceLifetime.Singleton);
            }
            if (!DIHelper.IsServiceRegistered<ISubscriptionService>(_container))
            {
                UseSqlSubscriptions();
            }
            var dcs = GetDefaultConnectionString();
            if (EnableSagas && !SendOnly)
            {
                if (!DIHelper.IsServiceRegistered<SagaStateHelper>(_container))
                {
                    _container.AddSingleton<SagaStateHelper>();
                }
                if (!DIHelper.IsServiceRegistered<ISagaRepository>(_container))
                {
                    string calias, tmp;
                    ConnectionStringInfo cs = null;
                    if (SqlUtil.ParseSqlEndpoint(Endpoint, out calias, out tmp))
                    {
                        cs = _connStrings.FirstOrDefault(x => x.Alias == calias);
                        if (cs == null) cs = SqlHelper.GetConnectionString(calias);
                    }
                    else throw new Exception("Endpoint");

                    DIHelper.RegisterService<SqlSagaStateRepository, ISagaRepository>(_container, ServiceLifetime.Singleton, sp =>
                    {
                        return new SqlSagaStateRepository
                        {
                            ConnectionString = cs == null ? calias : cs.ConnectionString,
                            ProviderName = cs == null ? DefaultDbProviderName : cs.ProviderName,
                            TableName = "NG_Sagas",
                            AutoCreateDatabase = AutoCreateQueues,
                            UseReceivingConnection = true
                        };
                    });
                }

            }
            if (!DIHelper.IsServiceRegistered<IMessageBus>(_container))
            {
                ConfigureSqlMessageBus();
            }

            return this;
        }

        
    }
}
