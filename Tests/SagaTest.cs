using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NGinnBPM.MessageBus;
using NGinnBPM.MessageBus.Sagas;
using NLog;

namespace Tests
{
    public class SagaMessage1
    {
        public string Id { get; set; }
        public int Num { get; set; }
    }

    public class SagaMessage2
    {
        public string Text { get; set; }
        public int Num { get; set; }
    }
    
    class SagaTest : Saga<SagaTest.MyState>, InitiatedBy<SagaMessage1>, IMessageConsumer<SagaMessage2>
    {
        private Logger log = LogManager.GetCurrentClassLogger();

        public class MyState
        {
            public List<int> Collected { get; set; }
            public DateTime Last { get; set; }

            public MyState()
            {
                Collected = new List<int>();
            }
        }

        protected override void Configure()
        {
            log.Info("Configuring saga");
            SagaId<SagaMessage1>(x => x.Id);
            SagaId<SagaMessage2>(x => x.Num.ToString());
        }



        public void Handle(SagaMessage1 message)
        {
            log.Info("SagaMessage1 arrived to saga {0}", Id);
            Data.Last = DateTime.Now;
            Data.Collected.Add(message.Num);
            System.Threading.Thread.Sleep(1000);
        }

        public void Handle(SagaMessage2 message)
        {
            log.Info("SagaMessage2 arrived to saga {0}", Id);
            System.Threading.Thread.Sleep(1000);
            Data.Collected.Add(message.Num);
            if (Data.Collected.Count > 10)
            {
                log.Info("COmpleting the saga {0}", Id);
                SetCompleted();
            }
        }
    }
}
