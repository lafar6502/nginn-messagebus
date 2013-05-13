using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BasicPingPongExample
{
    public class PingMessage
    {
        public string Text { get; set; }
        public int Generation { get; set; }
    }

    public class PongMessage
    {
        public string ReplyText { get; set; }
    }
}
