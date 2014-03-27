using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NGinnBPM.MessageBus.Sagas;
using Newtonsoft.Json;
using System.IO;
using System.Threading;
using NLog;
using System.Diagnostics;

namespace NGinnBPM.MessageBus.Impl.Sagas
{
    /// <summary>
    /// This handles retrieving and updating saga state
    /// and correlating the saga Id with messages
    /// </summary>
    public class SagaStateHelper
    {
        public ISagaRepository SagaStateRepo { get; set; }
        private HashSet<string> _currentlyProcessed = new HashSet<string>();
        private NLog.Logger log = NLog.LogManager.GetCurrentClassLogger();
        private static Logger statLog = LogManager.GetLogger("STAT.Saga");
        /// <summary>
        /// Resort to database-level record locking and don't put 
        /// an application level lock around saga operation.
        /// </summary>
        public bool UseDbRecordLocking { get; set; }
        /// <summary>
        /// if true, messages that have no saga id will be ignored
        /// if false, this will cause an error
        /// </summary>
        public bool IgnoreMessagesWithoutSagaId { get; set; }

        public SagaStateHelper()
        {
            UseDbRecordLocking = true;
            IgnoreMessagesWithoutSagaId = true;
        }

        public enum SagaDispatchResult
        {
            MessageHandled,
            ConcurrentUpdateHandleLater
        }

        private object _waiter = new object();

        private bool ExclusiveLock(string lockId, bool wait, Action act)
        {
            bool doLock = !(UseDbRecordLocking || string.IsNullOrEmpty(lockId));
            

            while (doLock)
            {
                lock (_waiter)
                {
                    if (_currentlyProcessed.Contains(lockId))
                    {
                        if (!wait) return false;
                        Monitor.Wait(_waiter);
                    }
                    else
                    {
                        _currentlyProcessed.Add(lockId);
                        break;
                    }
                }
            }
            try
            {
                NLog.MappedDiagnosticsContext.Set("nmbsaga", lockId);
                act();
            }
            finally
            {
                if (doLock)
                {
                    lock (_waiter)
                    {
                        _currentlyProcessed.Remove(lockId);
                        Monitor.PulseAll(_waiter);
                    }
                }
                NLog.MappedDiagnosticsContext.Remove("nmbsaga");
            }
            return true;
        }

        public SagaDispatchResult DispatchToSaga(string correlationId, object message, bool createNew, bool wait, SagaBase sagaHandler, Action<SagaBase> callback)
        {
            string sagaId = null;
            if (!sagaHandler.TryGetSagaIdFromMessage(message, out sagaId))
            {
                sagaId = correlationId;
            }
            else
            {
                if (SagaBase.IGNORE_MESSAGE == sagaId)
                {
                    return SagaDispatchResult.MessageHandled;
                }
            }

            if (!createNew && string.IsNullOrEmpty(sagaId))
            {
                if (IgnoreMessagesWithoutSagaId)
                {
                    return SagaDispatchResult.MessageHandled;
                }
                else throw new Exception("Saga Id could not be determined");
            }

            bool b = ExclusiveLock(sagaId, wait, delegate()
            {
                DispatchToSagaInternal(sagaId, createNew, sagaHandler, callback);
            });
            if (!b)
            {
                log.Info("Concurrent update of saga {0}, will process the message later", sagaId);    
            }
            return b ? SagaDispatchResult.MessageHandled : SagaDispatchResult.ConcurrentUpdateHandleLater;
        }

        

        protected void DispatchToSagaInternal(string sagaId, bool createNew, SagaBase sagaHandler, Action<SagaBase> callback)
        {
            string version = null;
            bool found = false; 
            object sagaState = null;
            var st = Stopwatch.StartNew();
            var sagaStateType = sagaHandler.GetType().BaseType.GetGenericArguments()[0]; //todo: what if inheritance is deeper???
            
            if (!string.IsNullOrEmpty(sagaId))
            {
                found = SagaStateRepo.Get(sagaId, sagaStateType, true, out sagaState, out version);
            }
            else //create new id
            {
                sagaId = Guid.NewGuid().ToString("N");
            }
            
            if (!found)
            {
                if (!createNew) throw new Exception("Saga instance not found: " + sagaId);
                sagaState = Activator.CreateInstance(sagaStateType);
                
                version = "1";
            }
            sagaHandler.IsNew = !found;
            sagaHandler.InitializeSagaState(sagaId, version, sagaState);
            callback(sagaHandler);
            if (sagaHandler.Completed)
            {
                if (!createNew)
                {
                    SagaStateRepo.Delete(sagaId);
                    log.Info("Deleted saga {0}/{1}", sagaHandler.GetType().Name, sagaId);
                }
            }
            else
            {
                sagaHandler.BeforeSave();
                if (!found)
                {
                    SagaStateRepo.InsertNew(sagaHandler.Id, sagaState);
                    log.Info("Saved new saga {0}/{1}", sagaHandler.GetType().Name, sagaId);
                }
                else
                {
                    SagaStateRepo.Update(sagaHandler.Id, sagaState, version);
                    log.Debug("Updated saga {0}/{1}", sagaHandler.GetType().Name, sagaId);
                }
            }
            st.Stop();
            statLog.Info("Dispatch:{0}", st.ElapsedMilliseconds);
        }
    }
}
