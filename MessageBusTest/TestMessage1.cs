using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NGinnBPM.MessageBus;
using NLog;

namespace Tests
{
    public class TestMessage3
    {
        public string KK { get; set; }
    }

    public class TestMessage2
    {
        public string Text { get; set; }
    }

    public class TestMessage1
    {
        public TestMessage1()
        {
            Id = 0;
        }

        public TestMessage1(int id)
        {
            Id = id;
        }
        public int Id { get; set; }
    }

    public class TestHandler : IMessageConsumer<TestMessage1>, IMessageConsumer<NGinnBPM.MessageBus.Messages.Pong>    {
        private Logger log;

        public TestHandler()
        {
            log = LogManager.GetLogger("TestHandler_" + this.GetHashCode());    
        }
        #region IMessageConsumer<TestMessage1> Members

        public void Handle(TestMessage1 message)
        {
            if (MessageBusContext.CurrentMessageBus != null)
            {
                log.Info("Handling message {0} from {1}", message.Id, MessageBusContext.CurrentMessageBus.Endpoint);
            }
            if (message.Id == 100) throw new Exception("hundred");
            
            //for (int i = 0; i < message.Id; i++)
            //    Bus.Reply(new NGinnBPM.MessageBus.Messages.Ping { Id = i.ToString() });
            //log.Info("TestMessage1 {0} ARRIVED", message.Id);
            if (message.Id % 2 > 5)
                throw new Exception("Should not arrive: " + message.Id);
            //System.Threading.Thread.Sleep(0);
        }

        #endregion


        #region IMessageConsumer<Pong> Members

        public IMessageBus Bus { get; set; }

        public void Handle(NGinnBPM.MessageBus.Messages.Pong message)
        {
            log.Info("PONG[{0}] from {1}. Roundtrip Time: {2}", message.Id, Bus.CurrentMessageInfo.Sender, DateTime.Now - message.PingSentDate);
        }

        #endregion
    }

    
}
