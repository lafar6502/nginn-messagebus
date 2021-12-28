using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NGinnBPM.MessageBus;
using NGinnBPM.MessageBus.Sagas;
using NLog;

namespace Tests
{
    public class GenerateDocumentBatch
    {
        public int N { get; set; }
    }

    public class DocumentBatchReadyEvent
    {
        public List<string> Documents { get; set; }
    }
    
    public class GenerateDocument
    {
        public string DocumentName { get; set; }
    }

    public class DocumentReadyEvent
    {
        public string DocumentName { get; set; }
    }



    class DocumentBatchSaga : Saga<DocumentBatchSaga.MyState>, 
        InitiatedBy<GenerateDocumentBatch>, 
        InitiatedBy<DocumentReadyEvent>
    {
        private Logger log = LogManager.GetCurrentClassLogger();

        public class MyState
        {
            public List<string> Collected { get; set; }
            public int BatchSize { get; set; }
        }

        protected override void Configure()
        {
            log.Info("Configuring saga");
            
            //we don't configure any ID lambdas because we're relying on correlation ID only 
        }



        public void Handle(GenerateDocumentBatch message)
        {
            Data.Collected = new List<string>();
            Data.BatchSize = message.N;
            for (int i = 0; i < Data.BatchSize; i++)
            {
                MessageBus.NewMessage(new GenerateDocument
                {
                    DocumentName = "Document#" + i
                }).SetCorrelationId(this.Id).Publish();
            }
        }

        public void Handle(DocumentReadyEvent message)
        {
            if (this.IsNew && string.IsNullOrEmpty(MessageBus.CurrentMessageInfo.CorrelationId))
            {
                log.Info("Correlation id not set - ignoring the message");
                SetCompleted();
                return;
            }
            Data.Collected.Add(message.DocumentName);
            if (Data.Collected.Count >= Data.BatchSize)
            {
                //we have collected all documents
                SetCompleted();
                MessageBus.Notify(new DocumentBatchReadyEvent
                {
                    Documents = Data.Collected
                });
            }
        }
    }
}
