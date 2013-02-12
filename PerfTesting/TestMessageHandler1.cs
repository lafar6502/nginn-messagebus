using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NGinnBPM.MessageBus;
using NLog;
using System.Configuration;

namespace PerfTesting
{
public class TestMessage1Reply
{
    public string RequestId { get; set; }
}

public class TestMessageHandler1 : IMessageConsumer<TestMessage1>
{
    private static Logger log = LogManager.GetCurrentClassLogger();
    
    public IMessageBus MessageBus { get; set; }

    public void Handle(TestMessage1 message)
    {
        log.Debug("Handling {0}", message.Id);
        if (!string.IsNullOrEmpty(System.Configuration.ConfigurationSettings.AppSettings["reply"]))
        {
            MessageBus.Send("sql://nginn/MQ_PT2", new TestMessage1Reply { RequestId = message.Id });
        }
    }
}


}
