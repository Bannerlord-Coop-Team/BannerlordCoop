using System;
using System.Reflection;

namespace Common;

public static class ModInformation
{
    public static bool IsServer { get; set; } = false;
    public static bool IsClient => !IsServer;

    /// <summary>
    /// Whether this process is a headless host — the bundled dedicated server or a container —
    /// as opposed to a graphical one. Headless hosts set BANNERLORD_USER_DIR to their data root
    /// before the coop container is built; graphical hosts never do. The same marker already
    /// keys the coop save root and the mod-config directory.
    /// </summary>
    /// <remarks>
    /// Read live rather than cached: the dedicated server sets the variable during its own
    /// start-up, which can run either side of the first read.
    /// </remarks>
    public static bool IsHeadlessHost =>
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("BANNERLORD_USER_DIR"));

    /// <summary>
    /// The mod build stamped on this assembly. Its semantic version comes from the same build
    /// property as the deployed module manifest.
    /// </summary>
    public static string BuildVersion { get; } = typeof(ModInformation).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
        ?.InformationalVersion ?? "0.0.0";

    /// <summary>The semantic portion of <see cref="BuildVersion"/>, used by Steam server metadata.</summary>
    public static Version Version { get; } = ParseVersion(BuildVersion);

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
}
