using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NGinnBPM.MessageBus;
using Castle.MicroKernel;

namespace NGinnBPM.MessageBus.Windsor
{
    public class WindsorServiceResolver : IServiceResolver
    {
        private IKernel _krnl;
        public WindsorServiceResolver(IKernel kernel)
        {
            _krnl = kernel;
        }

        #region IServiceResolver Members

        

        public object GetInstance(Type t)
        {
            return _krnl.Resolve(t);
        }

        public object GetInstance(Type t, string name)
        {
            return _krnl.Resolve(name, t);
        }

        public T GetInstance<T>() where T : class
        {
            return _krnl.Resolve<T>();
        }

        public T GetInstance<T>(string name) where T : class
        {
            return _krnl.Resolve<T>(name);
        }

        #endregion

        #region IServiceResolver Members

        public ICollection<object> GetAllInstances(Type t)
        {
            Array a = _krnl.ResolveAll(t);
            return new List<object>(a.Cast<object>());
        }

        public ICollection<T> GetAllInstances<T>() where T : class
        {
            return _krnl.ResolveAll<T>();
        }

        #endregion


        public bool HasService(Type t)
        {
            return _krnl.HasComponent(t);
        }

        public void ReleaseInstance(object instance)
        {
            _krnl.ReleaseComponent(instance);
        }
    }
}
