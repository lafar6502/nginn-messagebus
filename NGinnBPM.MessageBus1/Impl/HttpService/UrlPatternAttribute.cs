using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NGinnBPM.MessageBus.Impl.HttpService
{
    /// <summary>
    /// Apply this attribute to a servlet class to specify URL matching pattern for the servlet.
    /// This is not obligatory, you can specify the url pattern during servlet component registration.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class UrlPatternAttribute : Attribute
    {
        public UrlPatternAttribute(string pattern)
        {
            Pattern = pattern;
        }

        /// <summary>
        /// Url pattern
        /// </summary>
        public string Pattern { get; set; }

        /// <summary>
        /// Return url pattern for given type
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        public static string GetPatternForType(Type t)
        {
            UrlPatternAttribute att = (UrlPatternAttribute) Attribute.GetCustomAttribute(t, typeof(UrlPatternAttribute));
            return att != null ? att.Pattern : null;
        }
    }
}
