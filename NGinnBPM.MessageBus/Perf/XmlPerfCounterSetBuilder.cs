using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml;
using NLog;
using System.Reflection;
using System.Text.RegularExpressions;

namespace NGinnBPM.MessageBus.Perf
{
    public class XmlPerfCounterSetBuilder
    {
        private static Logger log = LogManager.GetCurrentClassLogger();

        public static IPerfCounterSet FromFile(string xmlFile)
        {
            XmlPerfCounterSetBuilder pcb = new XmlPerfCounterSetBuilder();
            return pcb.BuildFromFile(xmlFile);
        }

        public IPerfCounterSet BuildFromFile(string fileName)
        {
            XmlDocument doc = new XmlDocument();
            doc.Load(fileName);
            return BuildFromXml(doc.DocumentElement);
        }

        protected IPerfCounterSet BuildFromXml(XmlElement root)
        {
            PerfCounterSet pcs = new PerfCounterSet();
            pcs.AutoLogStatistics = true;
            if (root.HasAttribute("Name"))
                pcs.Name = root.GetAttribute("Name");
            if (root.HasAttribute("Debug"))
                pcs.Debug = "true".Equals(root.GetAttribute("Debug"));
            foreach (XmlNode n in root.ChildNodes)
            {
                XmlElement el = n as XmlElement;
                if (el != null)
                {
                    PerfCounterBase pcb = LoadFromXml(el);
                    if (pcb.Name == null)
                        throw new Exception("Missing perf counter name: " + el.OuterXml);
                    pcs.AddCounter(pcb);
                }
            }
            return pcs;
        }

        protected PerfCounterBase LoadFromXml(XmlElement el)
        {
            string n = el.LocalName;
            string tn = typeof(XmlPerfCounterSetBuilder).Namespace + "." + n;
            Type tp = Type.GetType(tn);
            if (tp == null)
                throw new Exception("Could not find performance counter type: " + tn);
            PerfCounterBase pcb = (PerfCounterBase) Activator.CreateInstance(tp);
            foreach (XmlAttribute attr in el.Attributes)
            {
                PropertyInfo pi = pcb.GetType().GetProperty(attr.LocalName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
                if (pi == null)
                    throw new Exception("Perf counter " + pcb.GetType().Name + ": missing property " + attr.LocalName);
                object val;
                if (pi.PropertyType == typeof(Regex))
                {
                    val = new Regex(el.GetAttribute(pi.Name));
                }
                else
                {
                    val = Convert.ChangeType(el.GetAttribute(pi.Name), pi.PropertyType);
                }
                pi.SetValue(pcb, val, null);
            }
            return pcb;
        }
    }
}
