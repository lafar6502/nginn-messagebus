using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NGinnBPM.MessageBus
{
    /// <summary>
    /// Object container / IoC abstraction
    /// used by nginn-messagebus for locating message handlers
    /// </summary>
    public interface IServiceResolver
    {
        //get all services that implement specified type of interface
        ICollection<object> GetAllInstances(Type t);
        //get all services that implement T
        ICollection<T> GetAllInstances<T>();
        //get service that implements specified interface
        object GetInstance(Type t);
        //get named service that implements specified interface
        object GetInstance(Type t, string name);
        T GetInstance<T>();
        T GetInstance<T>(string name);
        /// <summary>
        /// Check if a service of specified type is registered
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        bool HasService(Type t);
    }
}
