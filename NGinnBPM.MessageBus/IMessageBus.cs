using System;
using System.Collections.Generic;
using System.Text;

namespace NGinnBPM.MessageBus
{
    public interface CurrentMessageInfo
    {
        object Body { get; }
        string Sender { get; }
        string Destination { get; }
        string MessageId { get; }
        string CorrelationId { get; }
        IDictionary<string, string> Headers { get; }
        bool IsFinalRetry { get; }
    }

    /// <summary>
    /// Message publication mode
    /// </summary>
    public enum DeliveryMode
    {
        /// <summary>
        /// Default message delivery mode - durable, transactional and asynchronous
        /// </summary>
        DurableAsync,
        /// <summary>
        /// Local messages delivered asynchronously, without persistence
        /// </summary>
        LocalAsync,
        /// <summary>
        /// Messages delivered immediately (synchronously) to their handlers, without persistence
        /// </summary>
        LocalSync
    }

    /// <summary>
    /// Fluent interface for providing message information.
    /// You can use it to set message headers, delivery date, ttl etc.
    /// </summary>
    public interface ISendMessage
    {
        /// <summary>
        /// Set message body
        /// </summary>
        /// <param name="body"></param>
        /// <returns></returns>
        ISendMessage SetBody(object body);
        /// <summary>
        /// Send message batch
        /// </summary>
        /// <param name="messages"></param>
        /// <returns></returns>
        ISendMessage SetBatch(object[] messages);
        /// <summary>
        /// Set message correlation ID
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        ISendMessage SetCorrelationId(string id);
        /// <summary>
        /// Set message delivery date
        /// </summary>
        /// <param name="dt"></param>
        /// <returns></returns>
        ISendMessage SetDeliveryDate(DateTime dt);
        /// <summary>
        /// Set message time to live (not working yet)
        /// </summary>
        /// <param name="ttl"></param>
        /// <returns></returns>
        ISendMessage SetTTL(DateTime ttl);
        /// <summary>
        /// Mark message as belonging to sequence. This ensures that sequence messages will be delivered
        /// in order and system will not deliver next messages until all earlier messages have been
        /// successfully processed.
        /// Numbering of messages in a sequence should start at 1 and be sequential, without any gaps, 
        /// otherwise messages will not be delivered properly.
        /// Warning: this is not working now.
        /// </summary>
        /// <param name="sequenceId">Unique id of the sequence (make sure its unique)</param>
        /// <param name="seqNumber">Message number in sequence. </param>
        /// <param name="seqLength">Total length of the sequence. Specify null if unknown.</param>
        /// <returns></returns>
        ISendMessage InSequence(string sequenceId, int seqNumber, int? seqLength);
        /// <summary>
        /// Add custom message header.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        ISendMessage AddHeader(string name, string value);
        /// <summary>
        /// Set message label
        /// </summary>
        /// <param name="label"></param>
        /// <returns></returns>
        ISendMessage SetLabel(string label);
        /// <summary>
        /// Add another destination so the message
        /// will be sent to multiple endpoints.
        /// </summary>
        /// <param name="endpoint"></param>
        /// <returns></returns>
        ISendMessage AlsoSendTo(string endpoint);
        /// <summary>
        /// Deliver the message directly to local handlers,
        /// bypassing NGinn Messagebus queue. Message will
        /// not be persisted and will not be retried in case
        /// of failure. It will also not be a part of transaction
        /// so you can't rollback. This is for fire and forget messages
        /// </summary>
        /// <returns></returns>
        ISendMessage SetDeliveryMode(DeliveryMode b);
        /// <summary>
        /// Specify where the reply should be sent.
        /// Optional, by default reply will be sent to sender's endpoint.
        /// </summary>
        /// <param name="endpoint"></param>
        /// <returns></returns>
        ISendMessage ReplyTo(string endpoint);
        /// <summary>
        /// Send message in specified trasaction. Optional, use only if necessary.
        /// If your queues are in your database you can pass IDbTransaction here
        /// so the same transaction will be used for sending messages.
        /// Works only for sql transport.
        /// </summary>
        /// <param name="transaction"></param>
        /// <returns></returns>
        ISendMessage UseConnection(object conn);
        /// <summary>
        /// Mark message as high priority
        /// such message will be, if possible, processed before
        /// messages not marked as high priority
        /// Warning: marking all messages high priority will change nothing ;)
        /// </summary>
        /// <returns></returns>
        ISendMessage MarkHighPriority();
        /// <summary>
        /// Send message to specified endpoint
        /// </summary>
        /// <param name="endpoint"></param>
        void Send(string endpoint);

        
        /// <summary>
        /// Publish message
        /// </summary>
        void Publish();
    }

    /// <summary>
    /// Message bus interface
    /// </summary>
    public interface IMessageBus
    {
        void Notify(object msg);
        void Notify(object[] msgs);
        void Notify(object msg, DeliveryMode mode);
        void Send(string destination, object msg);
        void Send(string destination, object[] msgs);
        void NotifyAt(DateTime deliverAt, object msg);
        void SendAt(DateTime deliverAt, string destination, object msg);
        void Reply(object msg);
        string Endpoint
        {
            get;
        }
        /// <summary>
        /// Process current message again, later
        /// </summary>
        void HandleCurrentMessageLater(DateTime howLater);
        /// <summary>
        /// Current message information
        /// Valid only inside message handler.
        /// </summary>
        CurrentMessageInfo CurrentMessageInfo { get; }
        /// <summary>
        /// Subscribe for messages of messageType at specified endpoint
        /// Messages published at that endpoint will be delivered to this message bus.
        /// </summary>
        /// <param name="endpoint"></param>
        /// <param name="messageType"></param>
        void SubscribeAt(string endpoint, Type messageType);
        
        void UnsubscribeAt(string endpoint, Type messageType);
        /// <summary>
        /// Initialize a new message 
        /// Use a fluent interface to configure and send it.
        /// </summary>
        /// <returns></returns>
        ISendMessage NewMessage();
        /// <summary>
        /// Initialize a new message. Use the fluent interface
        /// to configure and send it.
        /// </summary>
        /// <param name="body"></param>
        /// <returns></returns>
        ISendMessage NewMessage(object body);
    }


    
    
}
