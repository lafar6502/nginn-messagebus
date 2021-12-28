using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NLog;
using NLog.Targets;

namespace NGinnBPM.MessageBus.Impl.HttpService
{
    [UrlPattern(@"^/log/(?<target>\w+)?")]
    class NLogLastDiagnosticMessagesServlet : IServlet
    {
        public string MatchUrl { get; set; }

        public void HandleRequest(IRequestContext ctx)
        {
            ctx.ResponseEncoding = Encoding.UTF8;
            
            string target;
            if (ctx.UrlVariables.TryGetValue("target", out target) && target.Length > 0)
            {
                var trg = LogManager.Configuration.FindTargetByName(target);
                if (trg == null)
                {
                    throw new NotFoundException();
                }
                if (!(trg is TailTarget))
                    throw new Exception("Only TailTarget is supported");
                TailTarget tt = (TailTarget)trg;
                string s = ctx.QueryString["lastMinutes"];
                var el = tt.GetLastEvents(string.IsNullOrEmpty(s) ? (DateTime?) null : DateTime.Now.AddMinutes(-Int32.Parse(s)));
                foreach(var e in el)
                {
                    ctx.Output.WriteLine("~{0}|{1}|{2}|{3}", e.Timestamp, e.Level, e.Source, e.Message);
                }
            }
            else
            {

                foreach (Target t in LogManager.Configuration.ConfiguredNamedTargets)
                {
                    if (t is TailTarget)
                        ctx.Output.WriteLine("<a href='./{0}'>{0}</a>", t.Name);
                }
                ctx.Output.WriteLine("----<br/>");
                ctx.Output.WriteLine("Use ?lastMinutes=NN query parameter to limit the output to last NN minutes");
            }
        }
    }

    /// <summary>
    /// Tail target collects last MaxMessages
    /// </summary>
    [Target("NGTail")]
    public class TailTarget : Target
    {
        private Queue<EventInfo> _lastMessages = new Queue<EventInfo>();

        public int MaxMessages { get; set; }

        public TailTarget()
        {
            MaxMessages = 100;
        }

        public class EventInfo
        {
            public DateTime Timestamp { get; set; }
            public string Level { get; set; }
            public string Source { get; set; }
            public string Message { get; set; }
        }

        protected override void Write(LogEventInfo logEvent)
        {
            _lastMessages.Enqueue(new EventInfo
            {
                Timestamp = logEvent.TimeStamp,
                Level = logEvent.Level.Name,
                Message = logEvent.FormattedMessage,
                Source = logEvent.LoggerName
            });
            while (_lastMessages.Count > MaxMessages)
                _lastMessages.Dequeue();
        }

        public IList<EventInfo> GetLastEvents(DateTime? laterThan)
        {
            var l = _lastMessages.ToList();
            l.Reverse();
            if (laterThan.HasValue)
            {
                l = l.FindAll(x => x.Timestamp >= laterThan.Value);
            }
            return l;
        }

        
    }
}
