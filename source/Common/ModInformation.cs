using System;
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

    /// <summary>Whether War Sails co-op runs in this process. Set at startup by CoopMod or the dedicated server, like <see cref="IsServer"/>.</summary>
    public static bool IsNavalEnabled { get; set; } = false;

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
