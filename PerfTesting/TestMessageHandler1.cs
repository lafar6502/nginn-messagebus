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

public class TestMessageHandler1 : 
    IMessageConsumer<TestMessage1>, 
    IMessageConsumer<TestMessageX>,
    IOutgoingMessageHandler<TestMessage1>
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

    public void Handle(TestMessageX message)
    {
        log.Info("Handling message {0}", message.Id);
        switch (message.Id % 3)
        {
            case 0:
                throw new Exception("Will retry message " + message.Id);
                break;
            case 1:
                MessageBus.HandleCurrentMessageLater(DateTime.Now.AddSeconds(10));
                break;
            default:
                break;
        }
    }

    public void OnMessageSend(TestMessage1 message)
    {
        log.Warn("**** OUT {0}", message.Id);
        MessageBus.Notify(new TestMessageX { Id = int.Parse(message.Id)});
    }
}


}
