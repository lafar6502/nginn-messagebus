using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NGinnBPM.MessageBus;

namespace Messages
{
    public class GreetingMessage 
    {
        public Guid Id { get; set; }
        public string Text { get; set; }
    }
}
