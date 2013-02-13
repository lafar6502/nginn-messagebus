using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Security;
using System.Web.SessionState;
using NGinnBPM.MessageBus;
using NGinnBPM.MessageBus.Windsor;

namespace WebPubSub
{
    public class Global : System.Web.HttpApplication
    {

        private MessageBusConfigurator mc;
        
        public static IMessageBus MessageBus { get; set; }


        void Application_Start(object sender, EventArgs e)
        {
            this.mc = MessageBusConfigurator.Begin()
                .ConfigureFromAppConfig()
                .UseSqlSubscriptions()
                .FinishConfiguration()
                .StartMessageBus();
            MessageBus = mc.GetMessageBus();
        }

        void Application_End(object sender, EventArgs e)
        {
            mc.StopMessageBus();
        }

        void Application_Error(object sender, EventArgs e)
        {

        }

        void Session_Start(object sender, EventArgs e)
        {
            // Code that runs when a new session is started

        }

        void Session_End(object sender, EventArgs e)
        {
            // Code that runs when a session ends. 
            // Note: The Session_End event is raised only when the sessionstate mode
            // is set to InProc in the Web.config file. If session mode is set to StateServer 
            // or SQLServer, the event is not raised.

        }

    }
}
