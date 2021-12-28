using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NGinnBPM.MessageBus.Impl.HttpService
{
    public class MimeTypes
    {
        static Dictionary<string, string> mimes = null;

        public static string GetMimeTypeForExtension(string ext)
        {
            if (mimes == null)
            {
                mimes = new Dictionary<string, string>();
                foreach (string s in mtypes)
                {
                    string[] ss = s.Split(':');
                    mimes[ss[0]] = ss[1];
                }
            }
            string mt;
            return mimes.TryGetValue(ext, out mt) ? mt : "application/text";
        }

        static string[] mtypes = {
                                     ".htm:text/html",
                                     ".html:text/html",
                                     ".txt:application/text"
                                 };
    }
}
