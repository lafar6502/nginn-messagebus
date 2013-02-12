using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NGinnBPM.MessageBus.Messages
{
    [Serializable]
    public class TestMessage
    {
        public TestMessage() 
        {
            Id = "-no ID-";
            ProcessingTime = 1;
        }

        public TestMessage(string id, bool fail, int processTime)
        {
            Id = id;
            FailMe = fail;
            ProcessingTime = processTime;
        }

        public string Id { get; set; }
        public bool FailMe { get; set; }
        public int ProcessingTime { get; set; }
    }
}
