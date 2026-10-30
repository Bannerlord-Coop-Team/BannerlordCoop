using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Coop.Core.Diagnostics;

public interface IShadowedAssemblyDetector
{
    IReadOnlyList<string> FindShadowingFiles(string appBaseDirectory);
}

/// <summary>
/// Finds files in the game's app base that the runtime binds instead of Coop's own copies,
/// because Coop.dll is loaded with LoadFrom and the app base is probed before the module bin.
/// </summary>
public class ShadowedAssemblyDetector : IShadowedAssemblyDetector
{
    private const string CommonFileName = "Common.dll";
    private const string HarmonyFileName = "0Harmony.dll";

    private readonly Version minimumHarmonyVersion;

    // Read from metadata so this doesn't load 0Harmony before the check
    public ShadowedAssemblyDetector()
        : this(typeof(ShadowedAssemblyDetector).Assembly.GetReferencedAssemblies()
            .FirstOrDefault(reference => reference.Name == "0Harmony")?.Version)
    {
    }

    public ShadowedAssemblyDetector(Version minimumHarmonyVersion)
    {
        this.minimumHarmonyVersion = minimumHarmonyVersion;
    }

    public IReadOnlyList<string> FindShadowingFiles(string appBaseDirectory)
    {
        var shadowingFiles = new List<string>();

        // Another mod's Common.dll replaces Coop's whatever its version, the assembly names match
        var commonPath = Path.Combine(appBaseDirectory, CommonFileName);
        if (File.Exists(commonPath))
        {
            shadowingFiles.Add(commonPath);
        }

        // A newer 0Harmony.dll (from BLSE or another tool) still has everything Coop calls, only an older one breaks it
        var harmonyPath = Path.Combine(appBaseDirectory, HarmonyFileName);
        if (File.Exists(harmonyPath) && IsOlderHarmony(harmonyPath))
        {
            shadowingFiles.Add(harmonyPath);
        }

        return shadowingFiles;
    }

    private bool IsOlderHarmony(string harmonyPath)
    {
        try
        {
            return AssemblyName.GetAssemblyName(harmonyPath).Version < minimumHarmonyVersion;
        }
        catch (Exception exception) when (exception is BadImageFormatException || exception is IOException)
        {
            // The runtime can't bind a file it can't read either, so Coop would still fail on it
            return true;
        }
    }
}
