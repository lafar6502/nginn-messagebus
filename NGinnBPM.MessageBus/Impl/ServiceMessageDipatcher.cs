using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using NLog;
using NGinnBPM.MessageBus.Sagas;
using System.Net;

namespace NGinnBPM.MessageBus.Impl
{
    public class ServiceInfo
    {
        public string Name { get; set; }
        public Type RequestType { get; set; }
        public Type ResponseType { get; set; }
    }

    public interface IServiceMessageDispatcher
    {
        object CallService(string name, object request);
        object CallService(object request);
        ServiceInfo GetServiceInfo(string name);
        IList<ServiceInfo> Services { get; }
    }
    /// <summary>
    /// Message dispatcher delivers messages to handlers
    /// </summary>
    public class ServiceMessageDispatcher : IServiceMessageDispatcher
    {

        public IServiceResolver ServiceLocator { get; set; }
        private Logger log = LogManager.GetCurrentClassLogger();

        public class ServiceHandlerInfo
        {
            public string ServiceName { get; set; }
            public Type MessageHandlerGenericType;
            public Type RequestType;
            public ServiceHandlerMethodDelegate HandleMethod;
            public string HandlerName;
        }

        private List<ServiceHandlerInfo> _services = null;

        private List<ServiceHandlerInfo> SetupDefaultServiceHandlers()
        {
            var l = new List<ServiceHandlerInfo>();
            var srvs = ServiceLocator.GetAllInstances<IMessageHandlerServiceBase>();
            foreach (var srv in srvs)
            {
                var st = srv.GetType();
                Type sit = null;
                foreach (var it in st.GetInterfaces())
                {
                    if (it.IsGenericType && it.GetGenericTypeDefinition() == typeof(IMessageHandlerService<>))
                    {
                        Type mtype = it.GetGenericArguments()[0];
                        string sname = mtype.Name;
                        if (l.Exists(x => x.ServiceName.Equals(sname, StringComparison.InvariantCultureIgnoreCase)))
                            sname = string.Format("{0}_{1}", st.Name, mtype.Name);
                        log.Info("Registering service handler {0} of message {1}. Service name: {2}", st.FullName, mtype.FullName, sname);
                        if (l.Exists(x => x.ServiceName.Equals(sname, StringComparison.InvariantCultureIgnoreCase))) throw new Exception("Failed to register service handler because of duplicate service name. " + st.FullName);
                        var ht = typeof(IMessageHandlerService<>).MakeGenericType(mtype);
                        l.Add(new ServiceHandlerInfo {
                            ServiceName = sname,
                            RequestType = mtype,
                            MessageHandlerGenericType = ht,
                            HandleMethod = DelegateFactory.CreateServiceHandlerDelegate(ht.GetMethod("Handle"))
                        });
                    }
                }
                ServiceLocator.ReleaseInstance(srv);
            }
            return l;
        }

        protected List<ServiceHandlerInfo> GetServices()
        {
            var s = _services;
            if (s != null) return s;
            s = SetupDefaultServiceHandlers();
            _services = s;
            return s;
        }

        protected ServiceHandlerInfo GetServiceHandler(string name)
        {
            return GetServices().Find(x => x.ServiceName == name);
        }
        
        

        public virtual object CallService(string name, object inputMessage)
        {
            if (ServiceCallContext.Current == null) throw new Exception("Service call context has not been set up!");
            ServiceHandlerInfo shi = null;
            var tp = inputMessage.GetType();
            
            if (!string.IsNullOrEmpty(name))
                shi = GetServiceHandler(name);
            else
                shi = GetServices().Find(x => x.RequestType == tp);

            if (shi == null) throw new Exception("Service not found");
            var h = shi.HandlerName == null ? ServiceLocator.GetInstance(shi.MessageHandlerGenericType) : ServiceLocator.GetInstance(shi.MessageHandlerGenericType, shi.HandlerName);
            if (h == null) throw new Exception("Service not found: " + name);

            try
            {
                if (h is SagaBase)
                {
                    object ret = null;
                    var helper = ServiceLocator.GetInstance<Sagas.SagaStateHelper>();
                    var b = helper.DispatchToSaga(null, inputMessage, true, true, (SagaBase)h, delegate(SagaBase saga)
                    {
                        ret = shi.HandleMethod(saga, inputMessage);
                    });
                    ServiceLocator.ReleaseInstance(helper);
                    if (b != Sagas.SagaStateHelper.SagaDispatchResult.MessageHandled) throw new Exception();
                    return ret;
                }
                else
                {
                    return shi.HandleMethod(h, inputMessage);
                }
            }
            catch (TargetInvocationException ex)
            {
                if (ex.InnerException != null) throw ex.InnerException;
                throw;
            }
            finally
            {
                if (h != null)
                {
                    ServiceLocator.ReleaseInstance(h);
                }
            }
        }

        public object CallService(object message)
        {
            return CallService(null, message);
        }


        public virtual ServiceInfo GetServiceInfo(string name)
        {
            var shi = GetServiceHandler(name);
            return new ServiceInfo
            {
                Name = shi.ServiceName,
                RequestType = shi.RequestType,
                ResponseType = null
            };
        }

        public virtual IList<ServiceInfo> Services
        {
            get 
            {
                var l = new List<ServiceInfo>();
                foreach (var shi in GetServices())
                {
                    l.Add(new ServiceInfo
                    {
                        Name = shi.ServiceName,
                        RequestType = shi.RequestType
                    });
                }
                return l;
            }
        }
    }

    /// <summary>
    /// Service client implementation for local calling
    /// </summary>
    public class LocalServiceClient : IServiceClient
    {
        private IServiceMessageDispatcher _dispatcher;
        private Dictionary<string, string> _h = new Dictionary<string, string>();
        
        public LocalServiceClient(IServiceMessageDispatcher dispatcher)
        {
            _dispatcher = dispatcher;
        }

        public T CallService<T>(string serviceName, object request)
        {
            return (T)CallService(serviceName, request, typeof(T));
        }

        public T CallService<T>(object request)
        {
            return (T)CallService(null, request, typeof(T));
        }

        public object CallService(string serviceName, object request, Type responseType)
        {
            ServiceCallContext.Current = new ServiceCallContext
            {
                Headers = _h,
                User = System.Threading.Thread.CurrentPrincipal
            };
            try
            {
                return _dispatcher.CallService(serviceName, request);
            }
            finally
            {
                ServiceCallContext.Current = null;
            }
        }


        public System.Net.ICredentials Credentials { get;set;}
        
        public void AddHeader(string name, string val)
        {
            _h[name] = val;
        }
    }

}
