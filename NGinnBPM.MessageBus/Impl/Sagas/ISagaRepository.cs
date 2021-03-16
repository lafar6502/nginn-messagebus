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
        /// <summary>
        /// in case the saga is forUpdate, and it exists but is locked by someone else
        /// we return false, but version will be not null
        /// </summary>
        /// <param name="id"></param>
        /// <param name="stateType"></param>
        /// <param name="forUpdate"></param>
        /// <param name="state"></param>
        /// <param name="version"></param>
        /// <returns></returns>
        bool Get(string id, Type stateType, bool forUpdate, bool nowait, out object state, out string version);
        void Update(string id, object state, string originalVersion);
        void Delete(string id);
        void InsertNew(string id, object state);
    }

    
}
