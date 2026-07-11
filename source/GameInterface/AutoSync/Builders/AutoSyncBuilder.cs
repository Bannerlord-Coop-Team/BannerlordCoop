using Common.Logging;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace GameInterface.AutoSync.Builders;

public class AutoSyncBuilder
{
    private static readonly ILogger Logger = LogManager.GetLogger<AutoSyncBuilder>();

    private readonly AutoSyncRegistry autoSyncRegistry;
    private readonly AutoSyncAssemblyInfoBuilder autoSyncAssemblyInfoBuilder;
    private readonly AutoSyncPatchBuilder autoSyncPatchBuilder;
    private readonly AutoSyncConstantsBuilder autoSyncConstantsBuilder;

    public AutoSyncBuilder(AutoSyncRegistry autoSyncRegistry, AutoSyncAssemblyInfoBuilder autoSyncAssemblyInfoBuilder, AutoSyncPatchBuilder autoSyncPatchBuilder, AutoSyncConstantsBuilder autoSyncConstantsBuilder)
    {
        this.autoSyncRegistry = autoSyncRegistry;
        this.autoSyncAssemblyInfoBuilder = autoSyncAssemblyInfoBuilder;
        this.autoSyncPatchBuilder = autoSyncPatchBuilder;
        this.autoSyncConstantsBuilder = autoSyncConstantsBuilder;
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
        if (AutoSyncConfiguration.ExportFiles && Directory.Exists(AutoSyncConfiguration.ExportPath))
        {
            // Directory.Delete throws UnauthorizedAccessException on read-only files
            // (.NET Framework limitation). Strip attributes on all files before deleting.
            foreach (var file in Directory.GetFiles(AutoSyncConfiguration.ExportPath, "*", SearchOption.AllDirectories))
            {
                try { File.SetAttributes(file, FileAttributes.Normal); }
                catch (FileNotFoundException ex)
                {
                    Logger.Warning("[AutoSync] SetAttributes: file not found (race?): {File} — {Message}", file, ex.Message);
                }
                catch (IOException ex)
                {
                    Logger.Warning("[AutoSync] SetAttributes IOException: {File} — {Message}", file, ex.Message);
                }
            }

            Directory.Delete(AutoSyncConfiguration.ExportPath, true);
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
            // The game and Coop assemblies added below are net472 builds whose metadata
            // references types in mscorlib/netstandard. CoreCLR ships both as facades;
            // without them in the reference set Roslyn cannot unify e.g. System.Exception
            // across assemblies ("type 'Exception' is defined in an assembly that is not
            // referenced ... mscorlib") and the whole AutoSync compilation fails.
            assemblies.Add(Assembly.Load("mscorlib"));
            assemblies.Add(Assembly.Load("netstandard"));
        }

        assemblies.Add(typeof(Enumerable).Assembly);
        assemblies.Add(typeof(Queue<>).Assembly);
        assemblies.Add(typeof(Console).Assembly);
        assemblies.Add(typeof(ILogger).Assembly);
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

        var syntaxTrees = autoSyncRegistry.Registrations
            .SelectMany(registration => autoSyncPatchBuilder.Build(registration.Key, registration.Value))
            .Append(autoSyncAssemblyInfoBuilder.Build(assemblies.Select(a => a.GetName().Name))).ToList();
        syntaxTrees = syntaxTrees.Append(autoSyncConstantsBuilder.Build()).ToList();

        // https://www.strathweb.com/2018/10/no-internalvisibleto-no-problem-bypassing-c-visibility-rules-with-roslyn/
        // Allow IgnoresAccessChecksTo for dynamic compilation
        var compilationOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            .WithMetadataImportOptions(MetadataImportOptions.All)
            .WithAllowUnsafe(true)
#if DEBUG
            .WithOptimizationLevel(OptimizationLevel.Debug);
#else
            .WithOptimizationLevel(OptimizationLevel.Release);
#endif

        var topLevelBinderFlagsProperty = typeof(CSharpCompilationOptions).GetProperty("TopLevelBinderFlags", BindingFlags.Instance | BindingFlags.NonPublic);
        topLevelBinderFlagsProperty.SetValue(compilationOptions, (uint)1 << 22);

        // Under a .NET Core host (the dedicated server) the net472-generated sync code references
        // the net472 BCL façades ([mscorlib 4.0.0.0]System.*, [System 4.0.0.0]...), which are not
        // among the loaded `assemblies` with those identities (Exception etc. live in
        // System.Private.CoreLib here, not mscorlib). Add the runtime's façade + impl assemblies so
        // the BCL resolves. Skipped on the net472 client, where the real BCL is already loaded.
        IEnumerable<string> runtimeFacades = Enumerable.Empty<string>();
        if (typeof(object).Assembly.GetName().Name != "mscorlib")
        {
            runtimeFacades = Directory.EnumerateFiles(
                Path.GetDirectoryName(typeof(object).Assembly.Location), "*.dll");
        }

        var dynamicAssembly = CSharpCompilation.Create(
            "AutoSync",
            syntaxTrees: syntaxTrees,
            references: assemblies.Select(a => a.Location).Concat(runtimeFacades)
                .Where(path => !string.IsNullOrEmpty(path) && File.Exists(path))
                .Distinct().Select(a => MetadataReference.CreateFromFile(a)),
            options: compilationOptions
        );

        using var assemblyStream = new MemoryStream();
        using var pdbStream = new MemoryStream();

        var emitOptions = new EmitOptions(
            debugInformationFormat: DebugInformationFormat.PortablePdb
        );

        var result = dynamicAssembly.Emit(
            peStream: assemblyStream,
            pdbStream: pdbStream,
            options: emitOptions
        );

        if (!result.Success)
        {
            var errors = result.Diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .Select(d => $"{d.Location} {d.GetMessage()}")
                .ToList();

            Logger.Error("[AutoSync] Compilation failed with {ErrorCount} error(s):\n{Errors}",
                errors.Count, string.Join("\n", errors));

            throw new InvalidOperationException(string.Join("\n", errors));
        }

        return Assembly.Load(assemblyStream.GetBuffer());
    }
}
