using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NGinnBPM.MessageBus.Sagas;
using NGinnBPM.MessageBus;


namespace Tests
{
/// <summary>
/// Application's request to send an SMS
/// </summary>
public class SendSMSRequest
{
    public string MSISDN { get; set; }
    public string Text { get; set; }
    public string MessageId { get; set; }
}

/// <summary>
/// SMS delivery report for the application
/// </summary>
public class SMSDeliveryReport
{
    public bool Delivered { get; set; }
    public string Reason { get; set; }
    public string MessageId { get; set; }
}

/// <summary>
/// Send a SMS to the SMS gateway
/// </summary>
public class SendSMSToGateway
{
    public string MSISDN { get; set; }
    public string Text { get; set; }
    public string SMSId { get; set; }
}

/// <summary>
/// SMS gateway's delivery status message
/// </summary>
public class GatewayDeliveryReport
{
    public bool Success { get; set; }
    public string SMSId { get; set; }
}

/// <summary>
/// Message for handling delivery report timeout
/// </summary>
public class SMSDeliveryTimeout
{
    public string MessageId { get; set; }
}

public class SmsSaga : Saga<SmsSaga.SmsState>, 
    InitiatedBy<SendSMSRequest>,
    IMessageConsumer<GatewayDeliveryReport>,
    IMessageConsumer<SMSDeliveryTimeout>

{
    public class SmsState
    {
        public DateTime MessageSentDate { get; set; }
        public bool DeliveryReportReceived { get; set; }
        public string SenderEndpoint { get; set; }
    }

    
    protected override void Configure()
    {
        //tell the message bus that saga ID will be passed in the MessageId field of SendSMSRequest message
        SagaId<SendSMSRequest>(x => x.MessageId);
        SagaId<GatewayDeliveryReport>(x => x.SMSId);
        SagaId<SMSDeliveryTimeout>(x => x.MessageId);
    }

    public void Handle(SendSMSRequest message)
    {
        //update saga state
        Data.MessageSentDate = DateTime.Now;
        Data.DeliveryReportReceived = false;
        Data.SenderEndpoint = MessageBus.CurrentMessageInfo.Sender; //store the sender's address for later use

        //send a request to the SMS gateway
        MessageBus.NewMessage(new SendSMSToGateway { MSISDN = message.MSISDN, SMSId = message.MessageId, Text = message.Text })
            .Send("sql://gateway/Queue");
        
        //and schedule a timeout message to be delivered in 3 days from now
        MessageBus.NewMessage(new SMSDeliveryTimeout { MessageId = message.MessageId })
            .SetDeliveryDate(DateTime.Now.AddDays(3))
            .Publish();


    }

    public void Handle(GatewayDeliveryReport message)
    {
        if (!Data.DeliveryReportReceived)
        {
            Data.DeliveryReportReceived = true;
            //send the report to the application
            MessageBus.NewMessage(new SMSDeliveryReport { Delivered = message.Success, Reason = message.Success ? "" : "Delivery failure", MessageId = Id })
                .Send(Data.SenderEndpoint);
        }
        //we could complete the saga here but let's wait for the timeout message
        //so it is processed without an error
    }

    public void Handle(SMSDeliveryTimeout message)
    {
        if (!Data.DeliveryReportReceived)
        {
            //inform the application about timeout
            MessageBus.NewMessage(new SMSDeliveryReport { Delivered = false, Reason = "Timeout", MessageId = this.Id })
                .Send(Data.SenderEndpoint);
        }
        SetCompleted(); //mark the saga as completed so it can be removed from the db
    }

    public object Handle(TestMessage1 message)
    {
        Console.WriteLine("TestMessage1 {1} in saga {0}", Id, message.Id);
        return "nic";
    }
}

}
