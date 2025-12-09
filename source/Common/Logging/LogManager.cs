﻿﻿﻿using Serilog;
using System;
using System.IO;
using System.Reflection;
using System.Linq;

namespace Common.Logging;

public static class LogManager
{
    public static LoggerConfiguration Configuration { get; set; }
    private static ILogger _logger;

    static LogManager()
    {
        var redirected = new string[]
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

        AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
        {
            var requested = new AssemblyName(args.Name).Name;
            if (!redirected.Contains(requested)) return null;

            var baseDir = Path.GetDirectoryName(typeof(LogManager).Assembly.Location);
            var candidate = Path.Combine(baseDir ?? string.Empty, requested + ".dll");
            if (File.Exists(candidate))
            {
                return Assembly.LoadFrom(candidate);
            }
            return null;
        };

        Configuration = new LoggerConfiguration();
    }

    private static ILogger EnsureLogger()
    {
        if (_logger == null)
        {
            _logger = Configuration
                .WriteTo.Sink(new OutputSinkManager())
                .CreateLogger();
        }
        return _logger;
    }

    public static void Build()
    {
        _logger = Configuration
            .WriteTo.Sink(new OutputSinkManager())
            .CreateLogger();
    }

    public static ILogger GetLogger<T>() => EnsureLogger().ForContext<T>();
}
