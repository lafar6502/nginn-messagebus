using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using NLog;
using System.IO;

namespace NGinnBPM.MessageBus.Impl.HttpService
{
    public class JsonServiceCallHandler
    {
        public IServiceMessageDispatcher ServiceDispatcher { get; set; }

        public void HandleServiceCall(string serviceName, string contentType, TextReader input, TextWriter output)
        {
            JsonSerializer ser = new JsonSerializer();
            ser.TypeNameHandling = TypeNameHandling.Objects;
            object request = null;
            object resp = null;
            if (!string.IsNullOrEmpty(serviceName))
            {
                var si = ServiceDispatcher.GetServiceInfo(serviceName);
                request = ser.Deserialize(input, si.RequestType);
                resp = ServiceDispatcher.CallService(serviceName, request);
            }
            else
            {
                request = ser.Deserialize(new JsonTextReader(input));
                resp = ServiceDispatcher.CallService(request);
            }
            ser.Serialize(output, resp);
        }

        
    }
}
