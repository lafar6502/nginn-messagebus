using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NGinnBPM.MessageBus;
using NGinnBPM.MessageBus.Windsor;

namespace DocumentBatchSaga
{
    class Program
    {
        static void Main(string[] args)
        {
            var mc = ConfigureMessageBus("sql://MessageBus/MQ_Events");
            var bus = mc.GetMessageBus();
            Console.WriteLine("Message bus is up, enter 1 for single doc or 2 for doc batch");
            var text = Console.ReadLine();
            while (text != "q")
            {
                if (text == "1")
                    bus.Notify(new GenerateDocument { DocumentName = "NO correlation" });
                else
                    bus.Notify(new GenerateDocumentBatch { N = 10 });
                Console.WriteLine("Message Sent. Enter more text or 'q' to quit");
                text = Console.ReadLine();
            }
            mc.StopMessageBus();
        }

        static MessageBusConfigurator ConfigureMessageBus(string endpoint)
        {
            var mc = NGinnBPM.MessageBus.Windsor.MessageBusConfigurator.Begin()
                .ConfigureFromAppConfig() //use connection strings from the app.config file
                .SetEndpoint(endpoint)
                .UseSqlSubscriptions()
                .AutoCreateDatabase(true) //queue tables will be created if they don't exist. Warning: you have to have 'create table' db permissions to do that!
                .SetEnableSagas(true) //disable saga for now
                .SetMaxConcurrentMessages(1)
                .AddMessageHandlersFromAssembly(typeof(Program).Assembly)
                //disable the 'distributor' for now: .AddMessageHandlersFromAssembly(typeof(EventDistributor).Assembly)
                .FinishConfiguration()
                .StartMessageBus(); //run the queue
            return mc;

        }
    }
}
