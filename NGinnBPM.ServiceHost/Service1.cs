using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.ServiceProcess;
using System.Text;
using NLog;
using Castle.Windsor;
using NGinnBPM.MessageBus;
using NGinnBPM.MessageBus.Windsor;
using Castle.Windsor.Configuration.Interpreters;
using Castle.Core.Resource;
using System.Configuration;
using System.IO;
using NLog;

namespace NGinnBPM.ServiceHost
{
    public partial class Service1 : ServiceBase
    {
        private MessageBusConfigurator _host;
        private Logger log = LogManager.GetCurrentClassLogger();

        public Service1()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            string componentConfig = ConfigurationManager.AppSettings["NGinnMessageBus.ServiceHost.ComponentConfig"];
            bool section = ConfigurationManager.GetSection(componentConfig) != null;
            WindsorContainer wc = null;
            if (!string.IsNullOrEmpty(componentConfig))
            {
                if (section)
                {
                    wc = new WindsorContainer(new XmlInterpreter(new ConfigResource(componentConfig)));
                }
                else if (Path.GetExtension(componentConfig) == "xml" || Path.GetExtension(componentConfig) == ".xml")
                {
                    log.Info("Configuring the container from xml file: {0}", componentConfig);
                    wc = new WindsorContainer(new XmlInterpreter(new FileResource(componentConfig, AppDomain.CurrentDomain.BaseDirectory)));
                }
                else throw new Exception("Don't know how to load config: " + componentConfig);
            }
            else wc = new WindsorContainer();
            _host = MessageBusConfigurator.Begin(wc)
                .ConfigureFromAppConfig()
                .AutoStartMessageBus(true)
                .FinishConfiguration();
        }

        protected override void OnStop()
        {
            if (_host != null)
            {
                _host.StopMessageBus();
                _host.Container.Dispose();
                _host = null;
            }
        }

        public void Start(string[] args)
        {
            OnStart(args);
        }
    }
}
