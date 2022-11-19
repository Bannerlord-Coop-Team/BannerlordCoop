using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Common.Extensions
{
    public static class ReflectionExtensions
    {
        public static IEnumerable<Type> GetDomainTypes(this AppDomain domain)
        {
            List<Type> types = new List<Type>();

            Assembly[] assemblies = domain.GetAssemblies();
            foreach (Assembly _assembly in assemblies)
            {
                try
                {
                    foreach (Type type in _assembly.GetTypes())
                    {
                        types.Add(type);
                    }
                }
                catch (ReflectionTypeLoadException) { }
            }

            return types;
        }
    }
}
