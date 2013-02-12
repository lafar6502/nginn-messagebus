using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NGinnBPM.MessageBus.Impl.HttpService;
using NLog;
using System.IO;

namespace NGinnBPM.MessageBus.Impl
{
    /// <summary>
    /// Provides message bus configuration and status information
    /// </summary>
    [UrlPattern(@"^/businfo/(?<cmd>(pause|resume|queues))?$")]
    public class MessageBusInfoServlet : ServletBase
    {
        public IMessageBus TheBus { get; set; }
        public SqlMessageTransport2 Transport { get; set; }
        public override void HandleRequest(IRequestContext ctx)
        {
            string cmd = ctx.UrlVariables["cmd"];
            if ("pause" == cmd)
            {
                Transport.PauseMessageProcessing = true;
                ctx.Output.WriteLine("paused");
                return;
            }
            else if ("resume" == cmd)
            {
                Transport.PauseMessageProcessing = false;
                ctx.Output.WriteLine("resumed");
                return;
            }
            else if ("queues" == cmd)
            {
                ctx.Output.WriteLine("input: {0}", Transport.InputQueueSize);
                ctx.Output.WriteLine("latency: {0}", Transport.AverageLatencyMs);

                return;
            }
            MessageBus mb = (MessageBus)TheBus;
            ctx.ResponseContentType = "text/html";
            ctx.Output.WriteLine("<html><body><table border='1'>");
            ctx.Output.WriteLine("<tr><td>Endpoint</td><td>{0}</td></tr>", mb.Endpoint);
            ctx.Output.WriteLine("<tr><td>Transport running</td><td align='right'>{0}</td></tr>", Transport.IsRunning);
            ctx.Output.Write("<tr><td>Message processing paused</td><td align='right'>{0}<br/>", Transport.PauseMessageProcessing);
            if (Transport.PauseMessageProcessing)
                ctx.Output.Write("<a href='./resume'>Resume</a>");
            else
                ctx.Output.Write("<a href='./pause'>Pause</a>");
            ctx.Output.WriteLine("</td></tr>");
            ctx.Output.WriteLine("<tr><td>Input queue</td><td align='right'>{0}</td></tr>", Transport.InputQueueSize);
            ctx.Output.WriteLine("<tr><td>Retry queue</td><td align='right'>{0}</td></tr>", Transport.RetryQueueSize);
            ctx.Output.WriteLine("<tr><td>Failed queue</td><td align='right'>{0}</td></tr>", Transport.FailQueueSize);
            ctx.Output.WriteLine("<tr><td>Message retention period</td><td align='right'>{0}</td></tr>", Transport.MessageRetentionPeriod);
            ctx.Output.WriteLine("<tr><td>Batch outgoing messages</td><td align='right'>{0}</td></tr>", mb.BatchOutgoingMessagesInTransaction);
            ctx.Output.WriteLine("</table>");
            
            ctx.Output.WriteLine("</body></html>");
        }
    }
}
