using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Transactions;


namespace NGinnBPM.MessageBus
{
    /// <summary>
    /// This class is used for exposing current message information
    /// </summary>
    public class MessageBusContext
    {
        
        [ThreadStatic]
        private static IMessageBus _curBus;

        /// <summary>
        /// Currently processed message or null if no message is currently processed
        /// </summary>
        public static CurrentMessageInfo CurrentMessage
        {
            get 
            {
                var cb = _curBus;
                return cb == null ? null : cb.CurrentMessageInfo; 
            }
        }

        /// <summary>
        /// Message bus that receives the current message
        /// </summary>
        public static IMessageBus CurrentMessageBus
        {
            get { return _curBus; }
            internal set { _curBus = value; }
        }

        [ThreadStatic]
        private static object _recvCon;
        [ThreadStatic]
        private static object _appCon;

        /// <summary>
        /// Database connection used for receiving current message
        /// This property is transport-specific, for sql
        /// transport it will be an SqlConnection object.
        /// It will be set only when processing an incoming message.
        /// This is an advanced functionality, should be used only because of specific performance reasons
        /// </summary>
        public static object ReceivingConnection
        {
            get
            {
                return _recvCon;
            }
            internal set
            {
                _recvCon = value;
            }
        }

        public static object AppManagedConnection
        {
            get
            {
                if (ProvideAppManagedConnection != null) return ProvideAppManagedConnection();
                return _appCon;
            }
            set
            {
                _appCon = value;
            }
        }
        
        public static Func<object> ProvideAppManagedConnection { get; set; }

        /// <summary>
        /// Get current transaction's outgoing messages in serialized form
        /// this is used for persisting state between transactions
        /// </summary>
        /// <returns></returns>
        public static string GetSerializedCurrentTransactionState()
        {
        	var t = Transaction.Current;
        	if (t == null) return null;
        	var mb = (Impl.MessageBus) CurrentMessageBus;
        	return mb.GetCurrentTransactionState();
        }

        /// <summary>
        /// returns true if there are uncommited messages in current transaction (sent but not commited yet)
        /// </summary>
        public static bool HasUncommitedMessagesInTransaction
        {
            get
            {
                var t = Transaction.Current;
                if (t == null) return false;
                var mb = (Impl.MessageBus) CurrentMessageBus;
                return mb.HasUncommitedMessages;
            }
        }
        
        /// <summary>
        /// Set current transaction state (or exactly, a list of outgoing messages to be sent on commit)
        /// </summary>
        /// <param name="state"></param>
        public static void SetCurrentTransactionState(string state)
        {
        	var t = Transaction.Current;
        	if (t == null) throw new Exception("No transaction");
        	var mb = (Impl.MessageBus) CurrentMessageBus;
        	mb.SetCurrentTransactionState(state);
        }
            
    }
}
