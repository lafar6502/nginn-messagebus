using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NGinnBPM.MessageBus;

namespace Tests
{

    public class TestServiceX : IMessageHandlerService<TestMessage1>
    {
        public object Handle(TestMessage1 message)
        {
            if (message.Id == 100) throw new Exception("hundred");
            return new TestMessage1 { Id = message.Id + 1 };
        }
    }
}
