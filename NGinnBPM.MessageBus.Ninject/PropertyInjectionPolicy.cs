using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Ninject.Components;
using Ninject.Injection;
using Ninject;
using Ninject.Selection.Heuristics;
using System.Reflection;

namespace NGinnBPM.MessageBus.NinjectConfig
{
    public class DefaultPropertyInjectionPolicy : NinjectComponent, IInjectionHeuristic
    {
        private readonly IKernel kernel;

        public IEnumerable<Assembly> KnownAssemblies { get; set; }

        public DefaultPropertyInjectionPolicy(IKernel kernel)
        {
            this.kernel = kernel;
        }

        public bool ShouldInject(MemberInfo memberInfo)
        {
            var propertyInfo = memberInfo as PropertyInfo;
            return ShouldInject(propertyInfo);
        }

        private bool ShouldInject(PropertyInfo propertyInfo)
        {
            if (propertyInfo == null)
                return false;

            if (!propertyInfo.CanWrite)
                return false;

            Type propertyType = propertyInfo.PropertyType;
            string assemblyName = propertyType.Assembly.GetName().Name;
            if (KnownAssemblies != null)
            {
                if (!KnownAssemblies.Contains(propertyType.Assembly)) return false;
            }
            var has = kernel.GetBindings(propertyType).FirstOrDefault() != null;
            return has;
        }
    }
}
