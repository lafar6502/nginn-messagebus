using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration.Install;
using System.Linq;


namespace NGinnBPM.ServiceHost
{
    [RunInstaller(true)]
    public partial class ProjectInstaller : System.Configuration.Install.Installer
    {
        public ProjectInstaller()
        {
            InitializeComponent();
            
        }

        protected override void OnBeforeInstall(IDictionary savedState)
        {
            string s = Context.Parameters.ContainsKey("servicename") ? Context.Parameters["servicename"] : null;
            if (!string.IsNullOrEmpty(s))
            {
                this.serviceInstaller1.ServiceName = s;
            }
            s = Context.Parameters.ContainsKey("account") ? Context.Parameters["account"] : null;
            if (!string.IsNullOrEmpty(s))
            {
                this.serviceProcessInstaller1.Account = (System.ServiceProcess.ServiceAccount)Enum.Parse(typeof(System.ServiceProcess.ServiceAccount), s);
            }
            base.OnBeforeInstall(savedState);
        }
    }
}
