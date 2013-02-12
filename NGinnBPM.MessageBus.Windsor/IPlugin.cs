using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Castle.Windsor;

namespace NGinnBPM.MessageBus.Windsor
{
    public interface IPlugin
    {
        void Register(IWindsorContainer wc);
        
        void OnFinishConfiguration(IWindsorContainer wc);
    }
}
