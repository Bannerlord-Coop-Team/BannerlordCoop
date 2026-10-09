using Common;
using Coop.Core.Client;
using GameInterface;
using Missions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Xunit;
using Xunit.Abstractions;

namespace Coop.Tests.Naval;

/// <summary>
/// Assemblies that load for every player must never reference NavalDLC or the naval projects that bind to it,
/// otherwise the mod fails to load for players without War Sails.
/// </summary>
public class NavalDlcIsolationTests
{
    private readonly ITestOutputHelper output;

    public NavalDlcIsolationTests(ITestOutputHelper output)
    {
        this.output = output;
    }

    public static IEnumerable<object[]> AlwaysLoadedAssemblies => new[]
    {
        new object[] { typeof(ModInformation).Assembly },
        new object[] { typeof(GameInterfaceModule).Assembly },
        new object[] { typeof(ClientModule).Assembly },
        new object[] { typeof(MissionModule).Assembly },
    };

    [Theory]
    [MemberData(nameof(AlwaysLoadedAssemblies))]
    public void AlwaysLoadedAssembly_DoesNotReferenceNavalDlc(Assembly assembly)
    {
        var navalReferences = assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .Where(IsNavalDlcDependent)
            .ToArray();

        Assert.True(navalReferences.Length == 0,
            $"{assembly.GetName().Name} references {string.Join(", ", navalReferences)}; move that code into Coop.Naval or Missions.Naval");
    }

    // The mod entry project references both naval projects only to ship their dlls, so it is read from disk without loading it
    [Fact]
    public void ModEntryAssembly_DoesNotReferenceNavalDlc()
    {
        var path = GetModEntryAssemblyPath();
        if (!File.Exists(path))
        {
            output.WriteLine($"Skipped: {path} was not built");
            return;
        }

        using var stream = File.OpenRead(path);
        using var peReader = new PEReader(stream);
        var metadata = peReader.GetMetadataReader();

        var navalReferences = metadata.AssemblyReferences
            .Select(handle => metadata.GetString(metadata.GetAssemblyReference(handle).Name))
            .Where(IsNavalDlcDependent)
            .ToArray();

        Assert.True(navalReferences.Length == 0,
            $"Coop references {string.Join(", ", navalReferences)}; move that code into Coop.Naval or Missions.Naval");
    }

    private static bool IsNavalDlcDependent(string assemblyName) =>
        assemblyName.StartsWith("NavalDLC", StringComparison.Ordinal) ||
        assemblyName == "Coop.Naval" ||
        assemblyName == "Missions.Naval";

    // Tests run from source/Coop.Tests/bin/<Configuration>/<TargetFramework>, the mod builds to source/Coop/bin/<Configuration>
    private static string GetModEntryAssemblyPath()
    {
        var targetFrameworkDirectory = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var configuration = targetFrameworkDirectory.Parent.Name;
        var sourceDirectory = targetFrameworkDirectory.Parent.Parent.Parent.Parent.FullName;

        return Path.Combine(sourceDirectory, "Coop", "bin", configuration, "Coop.dll");
    }
}
