using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Ninject;
using NGinnBPM.MessageBus.Impl;
using Ninject.Selection.Heuristics;


namespace NGinnBPM.MessageBus.NinjectConfig
{
    public class MessageBusConfigurator
    {
        private IKernel _k;
        protected MessageBusConfigurator(IKernel k)
        {
            _k = k;
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

       

        
    }
}
