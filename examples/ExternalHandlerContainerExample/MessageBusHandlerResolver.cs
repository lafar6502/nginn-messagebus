using System;
using System.Collections.Generic;
using System.Linq;
using NGinnBPM.MessageBus;
using NLog;
using SimpleInjector;

namespace ExternalHandlerContainerExample
{
    class MessageBusHandlerResolver : IServiceResolver
    {
        private readonly Container _container;
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public MessageBusHandlerResolver(SimpleInjector.Container container)
        {
            _container = container;
        }


        public ICollection<object> GetAllInstances(Type t)
        {
           
            try
            {
                var all = _container.GetAllInstances(t);
                return all.ToList();
            }
            catch (ActivationException e)
            {
                return new List<object>();
            }
        }

        public ICollection<T> GetAllInstances<T>() 
        {
            try
            {
                // Ask Raffal if reference type constraint to be added to IServiceResolver eg   where T : class
                //return _container.GetAllInstances<T>().ToList();
                
                return _container.GetAllInstances(typeof(T)).Cast<T>().ToList();
            }
            catch (ActivationException e)
            {
                return new List<T>();
            }
        }

        public object GetInstance(Type t)
        {
            return _container.GetInstance(t);
        }

        public object GetInstance(Type t, string name)
        {
            Logger.Warn(String.Format("Ignoring Name in call to GetAllInstance(Type,Name) for T: {0} N: {1}", t.Name, name));
            return _container.GetInstance(t);
        }

        public T GetInstance<T>()
        {
            if (typeof(T).IsValueType) throw new NotSupportedException(typeof(T).Name + "Is a Value Type which is not supported.");
            return (T)_container.GetInstance(typeof(T));
        }

        public T GetInstance<T>(string name)
        {
            Logger.Warn(String.Format("Ignoring Name in call to GetAllInstance<T>(Name) for T: {0} N: {1}", typeof(T).Name, name));
            if (typeof(T).IsValueType) throw new NotSupportedException(typeof(T).Name + "Is a Value Type which is not supported.");
            return (T)_container.GetInstance(typeof(T));
        }

        public bool HasService(Type t)
        {
            return _container.GetRegistration(t) != null;
        }

        public void ReleaseInstance(object inst)
        {
            Logger.Warn("Ignoring call ReleaseInstance() for T: " + inst.GetType().Name);
        }


    }
}