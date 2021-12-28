using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NGinnBPM.MessageBus.Impl.HttpService;
using System.IO;
using Newtonsoft.Json;

namespace NGinnBPM.MessageBus.Impl
{
    /// <summary>
    /// Http servlet for receiving nginnbpm messages
    /// Accepts POST messages in JSON format
    /// </summary>
    [UrlPattern(@"^(/$|/\?.+)")]
    public class MessageReceiverServlet : ServletBase
    {
        public HttpMessageTransport MessageForwarder { get; set; }
        
        
        public MessageReceiverServlet()
        {

        }

        public override void HandleRequest(IRequestContext ctx)
        {
            if (ctx.HttpMethod == "GET")
            {
                ctx.ResponseContentType = "text/html";
                ctx.Output.WriteLine("<p>This is NGinnBPM.MessageBus message receiver @{0}. POST a message here. To go to home page click <a href='index.htm'>here</a></p>", MessageForwarder.Endpoint);
            }
            else if (ctx.HttpMethod == "POST")
            {
                MessageForwarder.HandleIncomingMessage(ctx.Input);
            }
            else throw new Exception("Unknown method: " + ctx.HttpMethod);
        }
    }
}
