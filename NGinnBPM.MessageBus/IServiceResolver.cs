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
        ICollection<T> GetAllInstances<T>() where T : class;
        //get service that implements specified interface
        object GetInstance(Type t);
        //get named service that implements specified interface
        object GetInstance(Type t, string name);
        T GetInstance<T>() where T : class;
        T GetInstance<T>(string name) where T : class;
        /// <summary>
        /// Check if a service of specified type is registered
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        bool HasService(Type t);
        /// <summary>
        /// for releasing service instances.
        /// </summary>
        /// <param name="inst"></param>
        void ReleaseInstance(object inst);
    }
}
