using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.IO;

namespace Coop
{
    internal static class AssemblyHellscape
    {
        private static readonly string[] RedirectedAssemblies = new string[]
        {
            "Serilog",
            "Serilog.Sinks.File",
            "Serilog.Sinks.Debug",
            "Serilog.Enrichers.Process",
            "System.Diagnostics.DiagnosticSource",
            "System.Buffers",
            "System.Collections.Immutable",
            "System.IO.Pipelines",
            "System.Memory",
            "System.Numerics.Vectors",
            "System.Runtime.CompilerServices.Unsafe",
            "System.Text.Encodings.Web",
            "System.Text.Json",
            "System.Threading.Channels",
            "System.Threading.Tasks.Extensions",
            "Microsoft.Bcl.AsyncInterfaces"
        };


        /// <summary>
        /// Creates runtime binding redirects for any assembly listed in <see cref="RedirectedAssemblies"/>.
        /// </summary>
        public static void CreateAssemblyBindingRedirects()
        {
            AppDomain.CurrentDomain.AssemblyResolve += (sender, args) => {
                var requestedName = new AssemblyName(args.Name).Name;
                if (!RedirectedAssemblies.Contains(requestedName)) return null;

                var alreadyLoaded = AppDomain.CurrentDomain
                    .GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == requestedName);
                if (alreadyLoaded != null) return alreadyLoaded;

                try
                {
                    var baseDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                    var candidate = Path.Combine(baseDir ?? string.Empty, requestedName + ".dll");
                    if (File.Exists(candidate))
                    {
                        return Assembly.LoadFrom(candidate);
                    }
                }
                catch {}

                return null;
            };
        }
    }
}
