using System.Linq;
using System.Reflection;

namespace GameInterface.Services.Modules;

/// <summary>
/// Whether this build turns on War Sails (NavalDLC) co-op at all, like <c>IQuestFeatureFlag</c> does for quests.
/// </summary>
public interface INavalFeatureFlag
{
    // Off everywhere except nightly and local Debug builds
    bool IsEnabled { get; }

    // Whether this build compiled the naval code (CoopNavalDlc), whatever naval assemblies sit on disk
    bool IsNavalCodeBuilt { get; }
}

/// <summary>
/// War Sails co-op only runs in builds compiled with <c>TESTER</c>, which the nightly workflow and local Debug builds define.
/// </summary>
public class NavalFeatureFlag : INavalFeatureFlag
{
    // GameInterface.csproj stamps the CoopNavalDlc build property as assembly metadata
    private static readonly bool NavalCodeBuilt = typeof(NavalFeatureFlag).Assembly
        .GetCustomAttributes<AssemblyMetadataAttribute>()
        .Any(attribute => attribute.Key == "CoopNavalDlc" && IsTrueBuildProperty(attribute.Value));

#if TESTER
    public bool IsEnabled => true;
#else
    public bool IsEnabled => false;
#endif

    public bool IsNavalCodeBuilt => NavalCodeBuilt;

    // Reads a stamped MSBuild boolean, which MSBuild accepts in any case (-p:CoopNavalDlc=True)
    internal static bool IsTrueBuildProperty(string value) => bool.TryParse(value, out var result) && result;
}
