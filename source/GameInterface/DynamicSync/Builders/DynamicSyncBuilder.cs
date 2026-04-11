using Common.Logging;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace GameInterface.DynamicSync.Builders;

public class DynamicSyncBuilder
{
    private static readonly ILogger Logger = LogManager.GetLogger<DynamicSyncBuilder>();

    private readonly DynamicSyncRegistry dynamicSyncRegistry;
    private readonly DynamicSyncAssemblyInfoBuilder dynamicSyncAssemblyInfoBuilder;
    private readonly DynamicSyncPatchBuilder dynamicSyncPatchBuilder;

    public DynamicSyncBuilder(DynamicSyncRegistry dynamicSyncRegistry, DynamicSyncAssemblyInfoBuilder dynamicSyncAssemblyInfoBuilder, DynamicSyncPatchBuilder dynamicSyncPatchBuilder)
    {
        this.dynamicSyncRegistry = dynamicSyncRegistry;
        this.dynamicSyncAssemblyInfoBuilder = dynamicSyncAssemblyInfoBuilder;
        this.dynamicSyncPatchBuilder = dynamicSyncPatchBuilder;
    }

    public Assembly Build()
    {
        // Only the server/host process manages the export directory (ExportFiles=true by
        // default; the client sets it to false in StartAsClient before calling PatchAll).
        //
        // Why it is safe for the client never to delete or write the export directory:
        //
        //   - Same-machine (DebugAutoConnect): both processes share the same physical
        //     directory. Without this guard they raced on Directory.Delete, causing
        //     "directory is not empty" IOException. Since both processes generate identical
        //     output, the server's deletion and export is sufficient — the client gains
        //     nothing by also doing it.
        //
        //   - Separate machines (normal multiplayer): ExportPath is derived from
        //     Assembly.GetExecutingAssembly().Location, which is a local path on whichever
        //     machine the process runs on. Server and client therefore have completely
        //     independent directories on different disks — no race is possible regardless.
        //     The client skipping its own local export is a minor debug inconvenience at
        //     most; the server's exported sources are always available for inspection, and
        //     both sides generate identical code anyway.
        if (DynamicSyncConfiguration.ExportFiles && Directory.Exists(DynamicSyncConfiguration.ExportPath))
        {
            // Directory.Delete throws UnauthorizedAccessException on read-only files
            // (.NET Framework limitation). Strip attributes on all files before deleting.
            foreach (var file in Directory.GetFiles(DynamicSyncConfiguration.ExportPath, "*", SearchOption.AllDirectories))
            {
                try { File.SetAttributes(file, FileAttributes.Normal); }
                catch (FileNotFoundException ex)
                {
                    Logger.Warning("[DynamicSync] SetAttributes: file not found (race?): {File} — {Message}", file, ex.Message);
                }
                catch (IOException ex)
                {
                    Logger.Warning("[DynamicSync] SetAttributes IOException: {File} — {Message}", file, ex.Message);
                }
            }

            Directory.Delete(DynamicSyncConfiguration.ExportPath, true);
        }

        List<Assembly> assemblies = new List<Assembly>
        {
            Assembly.GetExecutingAssembly(),
        };

        // We need to load different dlls based on the runtime
        // currently the games runs .netframework 4.7.2
        // but tests uses .net 6.0
        if (Environment.Version.Major <= 4)
        {
            assemblies.Add(typeof(System.Collections.ArrayList).Assembly);
            assemblies.Add(typeof(System.Runtime.AssemblyTargetedPatchBandAttribute).Assembly);
        }
        else
        {
            assemblies.Add(Assembly.Load("System.Runtime"));
            assemblies.Add(Assembly.Load("System.Private.CoreLib"));
            assemblies.Add(Assembly.Load("System.Collections"));
            assemblies.Add(Assembly.Load("System.Reflection.Primitives"));
            assemblies.Add(Assembly.Load("System.Collections.Concurrent"));
        }

        assemblies.Add(typeof(Enumerable).Assembly);
        assemblies.Add(typeof(Queue<>).Assembly);
        assemblies.Add(typeof(Console).Assembly);
        assemblies.Add(Assembly.Load("System.Numerics.Vectors"));

        foreach (var asm in Assembly.GetExecutingAssembly().GetReferencedAssemblies())
        {
            assemblies.Add(Assembly.Load(asm.FullName));
        }

        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies()
            .Where(asm => !asm.IsDynamic)
            .Where(asm => !asm.FullName.Contains("Anonymously Hosted DynamicMethods Assembly"))
            .Where(asm => asm.FullName.Contains("AutoSyncAsm"))
        )
        {
            assemblies.Add(Assembly.Load(asm.FullName));
        }

        var syntaxTrees = dynamicSyncRegistry.Registrations
            .SelectMany(registration => dynamicSyncPatchBuilder.Build(registration.Key, registration.Value))
            .Append(dynamicSyncAssemblyInfoBuilder.Build(assemblies.Select(a => a.GetName().Name)));

        // https://www.strathweb.com/2018/10/no-internalvisibleto-no-problem-bypassing-c-visibility-rules-with-roslyn/
        // Allow IgnoresAccessChecksTo for dynamic compilation
        var compilationOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            .WithMetadataImportOptions(MetadataImportOptions.All)
            .WithAllowUnsafe(true);

        var topLevelBinderFlagsProperty = typeof(CSharpCompilationOptions).GetProperty("TopLevelBinderFlags", BindingFlags.Instance | BindingFlags.NonPublic);
        topLevelBinderFlagsProperty.SetValue(compilationOptions, (uint)1 << 22);

        var dynamicAssembly = CSharpCompilation.Create(
            "DynamicSync",
            syntaxTrees: syntaxTrees,
            references:
            assemblies.Select(a => a.Location).Distinct().Select(a => MetadataReference.CreateFromFile(a)),
            options: compilationOptions
        );

        using (var assemblyStream = new MemoryStream())
        using (var pdbStream = new MemoryStream())
        {
            var result = dynamicAssembly.Emit(assemblyStream, pdbStream);

            if (!result.Success)
            {
                var errors = result.Diagnostics
                    .Where(d => d.Severity == DiagnosticSeverity.Error)
                    .Select(d => $"{d.Location} {d.GetMessage()}")
                    .ToList();
                Logger.Error("[DynamicSync] Compilation failed with {ErrorCount} error(s):\n{Errors}",
                    errors.Count, string.Join("\n", errors));
                throw new InvalidOperationException(string.Join("\n", errors));
            }

            return Assembly.Load(assemblyStream.GetBuffer());
        };
    }
}
