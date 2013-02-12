using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NGinnBPM.MessageBus.Messages
{
    
    /// <summary>
    /// Ping message for testing
    /// </summary>
    [Serializable]
    public class Ping 
    {
        public Ping()
        {
            SentDate = DateTime.Now;
        }

        public string Id { get; set; }
        public DateTime SentDate { get; set; }
    }

    /// <summary>
    /// Reply message for Ping
    /// </summary>
    [Serializable]
    public class Pong 
    {
        public string Id { get; set; }
        public DateTime PingSentDate { get; set; }
        public DateTime SentDate { get; set; }
    }
}
