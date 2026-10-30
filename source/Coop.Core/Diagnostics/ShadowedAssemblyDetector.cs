using System.Collections.Generic;
using System.IO;

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
    // Another mod's Common.dll or an older 0Harmony.dll left in bin\Win64_Shipping_Client crashes the first loading screen
    private static readonly string[] OwnAssemblies = { "Common.dll", "0Harmony.dll" };

    public IReadOnlyList<string> FindShadowingFiles(string appBaseDirectory)
    {
        var shadowingFiles = new List<string>();
        foreach (var assembly in OwnAssemblies)
        {
            var path = Path.Combine(appBaseDirectory, assembly);
            if (File.Exists(path))
            {
                shadowingFiles.Add(path);
            }
        }

        return shadowingFiles;
    }
}
