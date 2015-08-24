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
                return _container.GetAllInstances(t).ToList();
            }
            catch (ActivationException e)
            {
                //activation error occurs if no instances are found.
                return new List<object>();
            }
        }

        public ICollection<T> GetAllInstances<T>() where T : class
        {
            try
            {
                return _container.GetAllInstances<T>().ToList();
            }
            catch (ActivationException e)
            {
                //activation error occurs if no instances are found.
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

        public T GetInstance<T>() where T : class
        {
            if (typeof(T).IsValueType) throw new NotSupportedException(typeof(T).Name + "Is a Value Type which is not supported.");
            return _container.GetInstance<T>();
        }

        public T GetInstance<T>(string name) where T : class
        {
            Logger.Warn(String.Format("Ignoring Name in call to GetAllInstance<T>(Name) for T: {0} N: {1}", typeof(T).Name, name));
            return _container.GetInstance<T>();
        }

        public bool HasService(Type t)
        {
            return _container.GetRegistration(t) != null;
        }

        public void ReleaseInstance(object inst)
        {
            //nothing to do
        }


    }
}