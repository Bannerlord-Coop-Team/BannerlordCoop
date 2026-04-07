using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace GameInterface.DynamicSync.Builders;

public class DynamicSyncBuilder
{
    private readonly DynamicSyncRegistry dynamicSyncRegistry;
    private readonly DynamicSyncAssemblyInfoBuilder dynamicSyncAssemblyInfoBuilder;
    private readonly DynamicSyncPatchBuilder dynamicSyncPatchBuilder;
    private readonly DynamicSyncConstantsBuilder dynamicSyncConstantsBuilder;

    public DynamicSyncBuilder(DynamicSyncRegistry dynamicSyncRegistry, DynamicSyncAssemblyInfoBuilder dynamicSyncAssemblyInfoBuilder, DynamicSyncPatchBuilder dynamicSyncPatchBuilder, DynamicSyncConstantsBuilder dynamicSyncConstantsBuilder)
    {
        this.dynamicSyncRegistry = dynamicSyncRegistry;
        this.dynamicSyncAssemblyInfoBuilder = dynamicSyncAssemblyInfoBuilder;
        this.dynamicSyncPatchBuilder = dynamicSyncPatchBuilder;
        this.dynamicSyncConstantsBuilder = dynamicSyncConstantsBuilder;
    }

    public Assembly Build()
    {
        if (Directory.Exists($@"{DynamicSyncConfiguration.ExportPath}"))
            Directory.Delete($@"{DynamicSyncConfiguration.ExportPath}", true);

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
            .Append(dynamicSyncAssemblyInfoBuilder.Build(assemblies.Select(a => a.GetName().Name))).ToList();
        syntaxTrees = syntaxTrees.Append(dynamicSyncConstantsBuilder.Build()).ToList();

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
                throw new InvalidOperationException(string.Join("\n", result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => $"{d.Location.ToString()} {d.GetMessage()}")));

            return Assembly.Load(assemblyStream.GetBuffer());
        };
    }
}
