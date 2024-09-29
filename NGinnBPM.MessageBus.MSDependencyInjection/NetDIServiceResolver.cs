using Microsoft.Extensions.DependencyInjection;
using NGinnBPM.MessageBus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NGinnBPM.MessageBus.MSDependencyInjection
{
    public class NetDIServiceResolver : IServiceResolver
    {
        private IServiceProvider _sp;
        public NetDIServiceResolver(IServiceProvider sp)
        {
            _sp = sp;
        }
        public ICollection<object> GetAllInstances(Type t)
        {
            return _sp.GetServices(t).Cast<object>().ToList();
        }

        public ICollection<T> GetAllInstances<T>() where T : class
        {
            return _sp.GetServices<T>().ToList();
        }

        public object GetInstance(Type t)
        {
            return _sp.GetService(t);
        }

        public object GetInstance(Type t, string name)
        {
            return _sp.GetKeyedServices(t, name).FirstOrDefault();
        }

        public T GetInstance<T>() where T : class
        {
            return _sp.GetService<T>();
        }

        public T GetInstance<T>(string name) where T : class
        {
            return _sp.GetKeyedService<T>(name);
        }

        public bool HasService(Type t)
        {
            return _sp.GetService(t) != null;
        }

        public void ReleaseInstance(object inst)
        {
            //pass
        }
    }
}
