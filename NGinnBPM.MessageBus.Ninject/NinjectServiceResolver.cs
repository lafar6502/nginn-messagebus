using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Ninject;
using NGinnBPM.MessageBus;

namespace NGinnBPM.MessageBus.NinjectConfig
{
    public class NinjectServiceResolver : IServiceResolver
    {
        private IKernel _krnl;
        public NinjectServiceResolver(IKernel krnl)
        {
            _krnl = krnl;
        }

        public ICollection<object> GetAllInstances(Type t)
        {
            return _krnl.GetAll(t).ToList();
        }

        public ICollection<T> GetAllInstances<T>()
        {
            return _krnl.GetAll<T>().ToList();
        }

        public object GetInstance(Type t)
        {
            return _krnl.Get(t);
        }

        public object GetInstance(Type t, string name)
        {
            return _krnl.Get(t, name);
        }

        public T GetInstance<T>()
        {
            return _krnl.Get<T>();
        }

        public T GetInstance<T>(string name)
        {
            return _krnl.Get<T>(name);
        }

        public bool HasService(Type t)
        {
            return _krnl.GetBindings(t).FirstOrDefault() != null;
        }


        public void ReleaseInstance(object inst)
        {
            _krnl.Release(inst);
        }
    }
}
