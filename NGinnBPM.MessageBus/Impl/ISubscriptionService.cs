using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NGinnBPM.MessageBus.Impl
{
    /// <summary>
    /// Messagebus subscription service
    /// </summary>
    public interface ISubscriptionService
    {
        /// <summary>
        /// Get list of endpoints where the message type should be forwarded.
        /// If the list contains "local" the message will be forwarded
        /// to local endpoint.
        /// Warning: this function checks only subscriptions for specified type name and doesn't
        /// look into its inheritance hierarchy or anything
        /// </summary>
        /// <param name="messageType"></param>
        /// <returns></returns>
        IEnumerable<string> GetTargetEndpoints(string messageType);
        /// <summary>
        /// Subscribe an endpoint for messages of specified type
        /// </summary>
        /// <param name="subscriberEndpoint"></param>
        /// <param name="messageType"></param>
        void Subscribe(string subscriberEndpoint, string messageType, DateTime? expiration);
        /// <summary>
        /// Unsubscribe an endpoint
        /// </summary>
        /// <param name="subscriberEndpoint"></param>
        /// <param name="messageType"></param>
        void Unsubscribe(string subscriberEndpoint, string messageType);

        void HandleSubscriptionExpirationIfNecessary(string subscriberEndpoint, string messageType);
    }
}
