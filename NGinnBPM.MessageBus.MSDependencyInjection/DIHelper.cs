using Microsoft.Extensions.DependencyInjection;
using NGinnBPM.MessageBus.Impl;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace NGinnBPM.MessageBus.MSDependencyInjection
{
    public class DIHelper
    {
        public static bool IsServiceRegistered(IServiceCollection wc, Type t)
        {
            return wc.Any(x => x.ServiceType == t);
        }

        public static bool IsServiceRegistered<T>(IServiceCollection c)
        {
            return IsServiceRegistered(c, typeof(T));
        }
        public static void RegisterMessageHandlersFromAssembly(Assembly asm, IServiceCollection c)
        {
            foreach (Type t in asm.GetTypes())
            {
                if (t.IsInterface || t.IsAbstract) continue;
                if (!t.GetInterfaces().Contains(typeof(DontAutoRegisterMe)) && !IsServiceRegistered(c, t))
                {
                    RegisterHandlerType(t, c, false);
                }
            }
        }

        public static void RegisterService(Type t, IEnumerable<Type> serviceInterfaces, IServiceCollection wc, ServiceLifetime lifetime)
        {
            var sd = ServiceDescriptor.Describe(t, t, lifetime);
            wc.Add(sd);
            foreach (var itf in serviceInterfaces)
            {
                if (itf == t) continue;
                var isd = ServiceDescriptor.Describe(itf, sp => sp.GetService(t), lifetime);
                wc.Add(isd);
            }
        }

        public static void RegisterService<T>(IEnumerable<Type> serviceInterfaces, IServiceCollection wc, ServiceLifetime lifetime, Func<IServiceProvider, T> factoryFun = null)
        {
            ServiceDescriptor sd;
            if (factoryFun != null)
            {
                sd = ServiceDescriptor.Describe(typeof(T), sp => factoryFun(sp), lifetime);
            }
            else
            {
                sd = ServiceDescriptor.Describe(typeof(T), typeof(T), lifetime);
            }
            wc.Add(sd);
            if (serviceInterfaces != null)
            {
                foreach (var itf in serviceInterfaces)
                {
                    var isd = ServiceDescriptor.Describe(itf, sp => sp.GetService<T>(), lifetime);
                    wc.Add(isd);
                }
            }
        }

        public static void RegisterService<T, I1>(IServiceCollection wc, ServiceLifetime lifetime, Func<IServiceProvider, T> factoryFun = null)
        {
            RegisterService<T>(new Type[] { typeof(I1) }, wc, lifetime, factoryFun);
        }

        public static void RegisterService<T, I1, I2>(IServiceCollection wc, ServiceLifetime lifetime, Func<IServiceProvider, T> factoryFun = null)
        {
            RegisterService<T>(new Type[] { typeof(I1), typeof(I2) }, wc, lifetime, factoryFun);
        }

        public static void RegisterService<T, I1, I2, I3>(IServiceCollection wc, ServiceLifetime lifetime, Func<IServiceProvider, T> factoryFun = null)
        {
            RegisterService<T>(new Type[] { typeof(I1), typeof(I2), typeof(I3) }, wc, lifetime, factoryFun);
        }
        /// <summary>
        /// register service with 4 interfaces
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="I1"></typeparam>
        /// <typeparam name="I2"></typeparam>
        /// <typeparam name="I3"></typeparam>
        /// <typeparam name="I4"></typeparam>
        /// <param name="wc"></param>
        /// <param name="lifetime"></param>
        /// <param name="factoryFun"></param>
        public static void RegisterService<T, I1, I2, I3, I4>(IServiceCollection wc, ServiceLifetime lifetime, Func<IServiceProvider, T> factoryFun = null)
        {
            RegisterService<T>(new Type[] { typeof(I1), typeof(I2), typeof(I3), typeof(I4) }, wc, lifetime, factoryFun);
        }

        /// <summary>
        /// for a given type, register all of its nginn-related interfaces as services
        /// </summary>
        /// <param name="t"></param>
        /// <param name="wc"></param>
        /// <param name="transient"></param>
        /// <param name="depends"></param>
        /// <exception cref="NotImplementedException"></exception>
        public static void RegisterHandlerType(Type t, IServiceCollection wc, bool? transient)
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

            var lt = ServiceLifetime.Singleton;
            if (transient.HasValue)
            {
                lt = transient.Value ? ServiceLifetime.Transient : ServiceLifetime.Singleton;
            }
            else
            {
                MessageHandlerConfigAttribute attr = (MessageHandlerConfigAttribute)Attribute.GetCustomAttribute(t, typeof(MessageHandlerConfigAttribute));
                if (attr != null) lt = attr.Transient ? ServiceLifetime.Transient : ServiceLifetime.Singleton;
            }

            RegisterService(t, l, wc, lt);

        }
    }
}
