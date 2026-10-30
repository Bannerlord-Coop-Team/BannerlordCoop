using System;
using System.Linq;
using System.Reflection;

namespace Common;

public static class ModInformation
{
    public static bool IsServer { get; set; } = false;
    public static bool IsClient => !IsServer;

    /// <summary>The module id of the War Sails DLC, the only DLC coop supports.</summary>
    public const string NavalDlcModuleId = "NavalDLC";

    /// <summary>Whether a module is the War Sails DLC; a non-DLC module that reuses its id is not.</summary>
    public static bool IsNavalDlcModule(string moduleId, bool isDlc) => isDlc && moduleId == NavalDlcModuleId;

    /// <summary>The naval battle assembly, shipped only by a build made with War Sails (CoopNavalDlc).</summary>
    public const string MissionsNavalAssemblyFile = "Missions.Naval.dll";

    /// <summary>The naval campaign assembly, shipped only by a build made with War Sails (CoopNavalDlc).</summary>
    public const string CoopNavalAssemblyFile = "Coop.Naval.dll";

    private static readonly string[] NavalAssemblyFiles = { MissionsNavalAssemblyFile, CoopNavalAssemblyFile };

    /// <summary>
    /// Whether this process supports NavalDLC: the module is enabled, this is a TESTER build and the coop naval code shipped.
    /// Set once at startup from <see cref="IsNavalSupported"/>; the module validator, host warning and battle gate read it.
    /// </summary>
    public static bool IsNavalDlcActive { get; set; }

    /// <summary>
    /// Whether NavalDLC is enabled, the build turns War Sails co-op on and every coop naval assembly is shipped,
    /// as told by <paramref name="isShipped"/>.
    /// </summary>
    public static bool IsNavalSupported(bool navalModuleEnabled, bool navalFeatureEnabled, Func<string, bool> isShipped)
    {
        return navalModuleEnabled && navalFeatureEnabled && NavalAssemblyFiles.All(isShipped);
    }

    /// <summary>
    /// The mod build stamped on this assembly. Its semantic version comes from the same build
    /// property as the deployed module manifest.
    /// </summary>
    public static string BuildVersion { get; } = typeof(ModInformation).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
        ?.InformationalVersion ?? "0.0.0";

    /// <summary>The semantic portion of <see cref="BuildVersion"/>, used by Steam server metadata.</summary>
    public static Version Version { get; } = ParseVersion(BuildVersion);

    /// <summary>The source commit appended to <see cref="BuildVersion"/> by the build.</summary>
    public static string Commit { get; } = ParseCommit(BuildVersion);

    /// <summary>Whether an advertised lobby was built with this exact mod build.</summary>
    public static bool MatchesBuildVersion(string version)
    {
        return string.Equals(BuildVersion, version, StringComparison.Ordinal);
    }

    private static Version ParseVersion(string buildVersion)
    {
        var semanticVersion = buildVersion.Split('-', '+')[0];
        return System.Version.TryParse(semanticVersion, out var version) ? version : new Version(0, 0, 0);
    }

    private static string ParseCommit(string buildVersion)
    {
        var separator = buildVersion.IndexOf('+');
        return separator >= 0 && separator < buildVersion.Length - 1
            ? buildVersion.Substring(separator + 1)
            : "unknown";
    }
}
