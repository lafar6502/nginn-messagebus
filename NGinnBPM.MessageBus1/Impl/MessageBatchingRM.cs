using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Transactions;
using NLog;

namespace NGinnBPM.MessageBus.Impl
{
    /// <summary>
    /// message batching resource manager 
    /// collects all messages sent in a transaction
    /// and inserts them to the database in one batch on commit.
    /// </summary>
    internal class MessageBatchingRM : ISinglePhaseNotification
    {

        public DateTime CreatedDate { get; private set; }
        public string TransactionId { get; set; }
        public IList<MessageContainer> Messages { get; set; }
        public bool TransactionOpen { get; private set; }
        private Action<MessageBatchingRM> _oncommit;
        private Action<MessageBatchingRM> _onrollback;
        private Action<MessageBatchingRM> _onprepare;

        public MessageBatchingRM(Action<MessageBatchingRM> onPrepare, Action<MessageBatchingRM> onCommit, Action<MessageBatchingRM> onRollback)
        {
            CreatedDate = DateTime.Now;
            Messages = new List<MessageContainer>();
            TransactionOpen = true;
            _oncommit = onCommit;
            _onrollback = onRollback;
            _onprepare = onPrepare;
        }

        private static Logger log = LogManager.GetCurrentClassLogger();

        #region IEnlistmentNotification Members

        public void Commit(Enlistment enlistment)
        {
            log.Debug("Commit {0}, Messages: {1}", TransactionId, Messages.Count);
            if (_oncommit != null) _oncommit(this);
            enlistment.Done();
            TransactionOpen = false;
        }

        public void InDoubt(Enlistment enlistment)
        {
            log.Warn("transaction in doubt {0}, messages {1}", TransactionId, this.Messages.Count);
            if (_onrollback != null) _onrollback(this);
            enlistment.Done();
            TransactionOpen = false;
        }

        public void Prepare(PreparingEnlistment preparingEnlistment)
        {
            try
            {
                log.Debug("Prepare {0}, Messages {1}. Sending the message batch.", TransactionId, Messages.Count);
                if (_onprepare != null) _onprepare(this);
                preparingEnlistment.Prepared();
            }
            catch (Exception ex)
            {
                log.Error("Error preparing transaction {0} ({1} messages): {2}", TransactionId, Messages.Count, ex);
                preparingEnlistment.ForceRollback(ex);
                TransactionOpen = false;
                if (_onrollback != null)
                {
                    try
                    {
                        _onrollback(this);
                    }
                    catch (Exception e2)
                    {
                        log.Error("Error performing rollback after a failed prepare: {0}", e2);
                    }
                }
            }
        }

        public void Rollback(Enlistment enlistment)
        {
            log.Warn("Rollback of transaction {0}, messages {1}", TransactionId, this.Messages.Count);
            if (_onrollback != null) _onrollback(this); 
            enlistment.Done();
            TransactionOpen = false;
            
        }

        #endregion

        #region ISinglePhaseNotification Members

        public void SinglePhaseCommit(SinglePhaseEnlistment singlePhaseEnlistment)
        {
            log.Debug("SinglePhaseCommit {0}", TransactionId);
            if (_onprepare != null) _onprepare(this);
            if (_oncommit != null) _oncommit(this);
            singlePhaseEnlistment.Committed();
            TransactionOpen = false;
        }

        #endregion
    }
}
