using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NGinnBPM.MessageBus.Sagas
{
    public interface InitiatedBy<T> : IMessageConsumer<T>
    {
        
    }

    
}
