using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NGinnBPM.MessageBus.Impl
{
    /// <summary>
    /// 'Dummy' placeholder of a subscription service
    /// </summary>
    public class DummySubscriptionService : ISubscriptionService
    {
        #region ISubscriptionService Members

        public IEnumerable<string> GetTargetEndpoints(string messageType)
        {
            return new List<string>(new string[] {"local"});
        }

        public void Subscribe(string subscriberEndpoint, string messageType, DateTime? expiration)
        {
        }

        public void Unsubscribe(string subscriberEndpoint, string messageType)
        {
        }

        #endregion


        public void HandleSubscriptionExpirationIfNecessary(string subscriberEndpoint, string messageType)
        {
            throw new NotImplementedException();
        }
    }
}
