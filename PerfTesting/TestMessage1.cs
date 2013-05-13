using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PerfTesting
{
    public class TestMessage1
    {
        public TestMessage1()
        {
            Ts = DateTime.Now;
        }

        public string Id { get; set; }
        public DateTime Ts { get; set; }
    }

    public class TestMessageX
    {
        public int Id { get;set;}
    }
}
