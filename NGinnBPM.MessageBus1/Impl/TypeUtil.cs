using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NGinnBPM.MessageBus.Impl
{
    /// <summary>
    /// Helper methods for retrieving message bus related interfaces
    /// </summary>
    public class TypeUtil
    {
        /// <summary>
        /// return all IMessageConsumer and IOutgoingMessageHandler - based
        /// interfaces supported by specified type
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        public static IList<Type> GetMessageHandlerInterfaces(Type t)
        {
            List<Type> l = new List<Type>();
            foreach (Type itf in t.GetInterfaces())
            {
                if (itf.IsGenericType)
                {
                    if (itf.GetGenericTypeDefinition() == typeof(IMessageConsumer<>) ||
                        itf.GetGenericTypeDefinition() == typeof(IOutgoingMessageHandler<>))
                    {
                        l.Add(itf);
                    }
                }
            }
            return l;
        }

        /// <summary>
        /// return all IMessageHandlerService - based interfaces implemented
        /// by specified type
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        public static IList<Type> GetMessageHandlerServiceInterfaces(Type t)
        {
            List<Type> l = new List<Type>();
            foreach (Type itf in t.GetInterfaces())
            {
                if (itf.IsGenericType)
                {
                    if (itf.GetGenericTypeDefinition() == typeof(IMessageHandlerService<>))
                    {
                        l.Add(itf);
                    }
                }
            }
            if (l.Count > 0 && !l.Contains(typeof(IMessageHandlerServiceBase))) l.Add(typeof(IMessageHandlerServiceBase));
            return l;
        }

        /// <summary>
        /// Check if type is a saga 
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        public static bool IsSagaType(Type t)
        {
            return typeof(NGinnBPM.MessageBus.Sagas.SagaBase).IsAssignableFrom(t);
        }


    }
}
