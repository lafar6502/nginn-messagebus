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
        private IMessageBus _theBus;
        private SqlMessageTransport2 _transport;

        public MessageBusInfoServlet(IMessageBus bus, SqlMessageTransport2 transport)
        {
            _theBus = bus;
            _transport = transport;
        }

        public override void HandleRequest(IRequestContext ctx)
        {
            string cmd = ctx.UrlVariables["cmd"];
            if ("pause" == cmd)
            {
                _transport.PauseMessageProcessing = true;
                ctx.Output.WriteLine("paused");
                return;
            }
            else if ("resume" == cmd)
            {
                _transport.PauseMessageProcessing = false;
                ctx.Output.WriteLine("resumed");
                return;
            }
            else if ("queues" == cmd)
            {
                ctx.Output.WriteLine("input: {0}", _transport.InputQueueSize);
                ctx.Output.WriteLine("latency: {0}", _transport.AverageLatencyMs);

                return;
            }
            MessageBus mb = (MessageBus)_theBus;
            ctx.ResponseContentType = "text/html";
            ctx.Output.WriteLine("<html><body><table border='1'>");
            ctx.Output.WriteLine("<tr><td>Endpoint</td><td>{0}</td></tr>", mb.Endpoint);
            ctx.Output.WriteLine("<tr><td>Transport running</td><td align='right'>{0}</td></tr>", _transport.IsRunning);
            ctx.Output.Write("<tr><td>Message processing paused</td><td align='right'>{0}<br/>", _transport.PauseMessageProcessing);
            if (_transport.PauseMessageProcessing)
                ctx.Output.Write("<a href='./resume'>Resume</a>");
            else
                ctx.Output.Write("<a href='./pause'>Pause</a>");
            ctx.Output.WriteLine("</td></tr>");
            ctx.Output.WriteLine("<tr><td>Input queue</td><td align='right'>{0}</td></tr>", _transport.InputQueueSize);
            ctx.Output.WriteLine("<tr><td>Retry queue</td><td align='right'>{0}</td></tr>", _transport.RetryQueueSize);
            ctx.Output.WriteLine("<tr><td>Failed queue</td><td align='right'>{0}</td></tr>", _transport.FailQueueSize);
            ctx.Output.WriteLine("<tr><td>Message retention period</td><td align='right'>{0}</td></tr>", _transport.MessageRetentionPeriod);
            ctx.Output.WriteLine("<tr><td>Batch outgoing messages</td><td align='right'>{0}</td></tr>", mb.BatchOutgoingMessagesInTransaction);
            ctx.Output.WriteLine("</table>");
            
            ctx.Output.WriteLine("</body></html>");
        }
    }
}
