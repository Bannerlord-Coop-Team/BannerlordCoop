using Common;
using System.Linq;
using System.Reflection;

namespace GameInterface.Services.Modules;

/// <summary>
/// Whether this build can run War Sails (NavalDLC) co-op.
/// </summary>
public interface INavalFeatureFlag
{
    // Whether this build compiled the naval code (CoopNavalDlc), whatever naval assemblies sit on disk
    bool IsNavalCodeBuilt { get; }

    // Why this build can't run War Sails, or null when it can; both naval assemblies next to assemblyDirectory must load
    string GetUnsupportedReason(string assemblyDirectory);
}

/// <summary>
/// War Sails co-op runs in builds compiled with the naval code whose naval assemblies load.
/// </summary>
public class NavalFeatureFlag : INavalFeatureFlag
{
    private static readonly string[] NavalAssemblyFiles = { ModInformation.MissionsNavalAssemblyFile, ModInformation.CoopNavalAssemblyFile };

    // GameInterface.csproj stamps the CoopNavalDlc build property as assembly metadata
    private static readonly bool NavalCodeBuilt = typeof(NavalFeatureFlag).Assembly
        .GetCustomAttributes<AssemblyMetadataAttribute>()
        .Any(attribute => attribute.Key == "CoopNavalDlc" && IsTrueBuildProperty(attribute.Value));

    public bool IsNavalCodeBuilt => NavalCodeBuilt;

    // Checks the build first, because naval assemblies left by an earlier deploy don't count for a build without naval code.
    public string GetUnsupportedReason(string assemblyDirectory)
    {
        if (!IsNavalCodeBuilt) return "this build was made without CoopNavalDlc";

        var loader = new NavalAssemblyLoader(assemblyDirectory);
        bool loads = NavalAssemblyFiles.All(file => loader.TryLoad(file, out _));
        return loads ? null : "a naval assembly is missing or failed to load";
    }

    // Reads a stamped MSBuild boolean, which MSBuild accepts in any case (-p:CoopNavalDlc=True)
    internal static bool IsTrueBuildProperty(string value) => bool.TryParse(value, out var result) && result;
}
