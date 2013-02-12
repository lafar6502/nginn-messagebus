using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NGinnBPM.MessageBus;
using NGinnBPM.MessageBus.Sagas;

namespace NGinnBPM.MessageBus.Impl.Sagas
{
    

    public interface ISagaRepository
    {
        bool Get(string id, Type stateType, bool forUpdate, out object state, out string version);
        void Update(string id, object state, string originalVersion);
        void Delete(string id);
        void InsertNew(string id, object state);
    }

    
}
