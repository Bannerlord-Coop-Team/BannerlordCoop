using GameInterface.Services.Modules;
using Mono.Cecil;
using System;
using System.IO;
using Xunit;
using CecilTypeAttributes = Mono.Cecil.TypeAttributes;

namespace GameInterface.Tests.Services.Modules;

/// <summary>
/// Covers loading an optional naval assembly, where a missing, corrupt or dependency-broken file must read as unloadable.
/// </summary>
public class NavalAssemblyLoaderTests : IDisposable
{
    private const string AssemblyFile = "Missions.Naval.dll";

    private readonly string directory = Path.Combine(Path.GetTempPath(), $"naval-loader-{Guid.NewGuid():N}");

    // Gives each test its own empty folder to ship assemblies into.
    public NavalAssemblyLoaderTests()
    {
        Directory.CreateDirectory(directory);
    }

    // Removes the test folder; a loaded assembly can keep its file locked, which doesn't matter for the next test.
    public void Dispose()
    {
        try { Directory.Delete(directory, recursive: true); }
        catch (UnauthorizedAccessException) { }
        catch (IOException) { }
    }

    [Fact]
    public void TryLoad_MissingFile_IsFalse()
    {
        Assert.False(new NavalAssemblyLoader(directory).TryLoad(AssemblyFile, out var assembly));
        Assert.Null(assembly);
    }

    [Fact]
    public void TryLoad_ZeroByteFile_IsFalse()
    {
        File.WriteAllBytes(Path.Combine(directory, AssemblyFile), Array.Empty<byte>());

        Assert.False(new NavalAssemblyLoader(directory).TryLoad(AssemblyFile, out var assembly));
        Assert.Null(assembly);
    }

    [Fact]
    public void TryLoad_NotAnAssembly_IsFalse()
    {
        File.WriteAllBytes(Path.Combine(directory, AssemblyFile), new byte[] { 0x4D, 0x5A, 0x00, 0x01, 0x02, 0x03 });

        Assert.False(new NavalAssemblyLoader(directory).TryLoad(AssemblyFile, out var assembly));
        Assert.Null(assembly);
    }

    // A partial deploy can ship the naval dll without a dependency; its types then fail to resolve.
    [Fact]
    public void TryLoad_TypeFromMissingDependency_IsFalse()
    {
        WriteAssembly(Path.Combine(directory, AssemblyFile), withMissingDependency: true);

        Assert.False(new NavalAssemblyLoader(directory).TryLoad(AssemblyFile, out var assembly));
        Assert.Null(assembly);
    }

    [Fact]
    public void TryLoad_ValidAssembly_ReturnsIt()
    {
        string path = Path.Combine(directory, AssemblyFile);
        WriteAssembly(path, withMissingDependency: false);

        Assert.True(new NavalAssemblyLoader(directory).TryLoad(AssemblyFile, out var assembly));
        Assert.NotNull(assembly.GetType("Probe.Shipped"));
    }

    // Writes a uniquely named assembly with one public type, optionally deriving from a type in an assembly that doesn't exist.
    internal static void WriteAssembly(string path, bool withMissingDependency)
    {
        string name = $"NavalProbe{Guid.NewGuid():N}";
        var definition = AssemblyDefinition.CreateAssembly(new AssemblyNameDefinition(name, new Version(1, 0, 0, 0)), name, ModuleKind.Dll);
        var module = definition.MainModule;

        TypeReference baseType = module.TypeSystem.Object;
        if (withMissingDependency)
        {
            var missing = new AssemblyNameReference($"MissingNavalDependency{Guid.NewGuid():N}", new Version(1, 0, 0, 0));
            module.AssemblyReferences.Add(missing);
            baseType = new TypeReference("Missing", "Base", module, missing);
        }

        module.Types.Add(new TypeDefinition("Probe", "Shipped", CecilTypeAttributes.Public | CecilTypeAttributes.Class, baseType));
        definition.Write(path);
    }
}
