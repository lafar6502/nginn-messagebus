using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using NGinnBPM.MessageBus;

namespace WebPubSub
{
    public partial class _Default : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            Global.MessageBus.Notify(new WebPubSub.Messages.RequestNotification
            {
                Url = this.Request.RawUrl,
                ClientIP = this.Request.UserHostAddress
            });
        }
    }
}
