using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace MissionTestMod
{
    internal static class AssemblyHellscape
    {
        private static readonly string[] RedirectedAssemblies = new string[]
        {
            "System.Collections.Immutable",
            "System.Runtime.CompilerServices.Unsafe",
            "Microsoft.Bcl.AsyncInterfaces",
            "System.Threading.Tasks.Extensions",
        };

        private static readonly Dictionary<string, Assembly> LoadedRedirects = RedirectedAssemblies
            .ToDictionary(str => str, str => AppDomain.CurrentDomain.Load(str));

        /// <summary>
        /// Creates runtime binding redirects for any assembly listed in <see cref="RedirectedAssemblies"/>.
        /// </summary>
        public static void CreateAssemblyBindingRedirects()
        {
            AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
            {
                var requestedAssembly = new AssemblyName(args.Name);
                if (LoadedRedirects.TryGetValue(requestedAssembly.Name, out Assembly assembly))
                {
                    return assembly;
                }

                return null;
            };
        }
    }
}
