using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NGinnBPM.MessageBus
{
    public interface IServiceResolver
    {
        ICollection<object> GetAllInstances(Type t);
        ICollection<T> GetAllInstances<T>();
        object GetInstance(Type t);
        object GetInstance(Type t, string name);
        T GetInstance<T>();
        T GetInstance<T>(string name);
    }
}
