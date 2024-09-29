using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SimpleInjector;

namespace NGinnBPM.MessageBus.SI
{
    public class SimpleInjectorServiceResolver : IServiceResolver
    {
        private Container _c;
        public SimpleInjectorServiceResolver(Container c)
        {
            _c = c;
        }
        public ICollection<object> GetAllInstances(Type t)
        {
            return _c.GetAllInstances(t).ToList();
        }

        public ICollection<T> GetAllInstances<T>() where T : class
        {
            return _c.GetAllInstances<T>().ToList();
        }

        public object GetInstance(Type t)
        {
            return _c.GetInstance(t);
        }

        public object GetInstance(Type t, string name)
        {
            return 
        }

        public T GetInstance<T>() where T : class
        {
            throw new NotImplementedException();
        }

        public T GetInstance<T>(string name) where T : class
        {
            throw new NotImplementedException();
        }

        public bool HasService(Type t)
        {
            throw new NotImplementedException();
        }

        public void ReleaseInstance(object inst)
        {
            throw new NotImplementedException();
        }
    }
}
