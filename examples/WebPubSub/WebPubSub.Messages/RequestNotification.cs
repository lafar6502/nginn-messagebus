using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WebPubSub.Messages
{
    public class RequestNotification
    {
        public string Url { get; set; }
        public string ClientIP { get; set; }
    }
}
