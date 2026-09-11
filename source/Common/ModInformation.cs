using System;
using System.Reflection;

namespace Common;

public static class ModInformation
{
    public static bool IsServer { get; set; } = false;
    public static bool IsClient => !IsServer;
#if DEBUG
    public const string NavalLabCapabilityPrefix = "Coop.Debug.NavalLab.v1.";
    public static string NavalLabCapability { get; private set; }
    public static bool IsNavalLab => NavalLabCapability != null;

    public static void ConfigureNavalLab(string optIn, string runToken, bool navalDlcActive)
    {
        if (optIn == null) return;
        const string prefix = "new-campaign:";
        if (!optIn.StartsWith(prefix, StringComparison.Ordinal)
            || !Guid.TryParseExact(optIn.Substring(prefix.Length).Trim(), "D", out var nonce)
            || nonce == Guid.Empty || string.IsNullOrWhiteSpace(runToken) || runToken.Length > 64
            || !System.Linq.Enumerable.All(runToken, c => char.IsLetterOrDigit(c) || c == '-' || c == '_')
            || !navalDlcActive)
            throw new InvalidOperationException("Naval lab opt-in requires new-campaign:<nonce UUID>, a valid /cooptestrun scope and active NavalDLC.");
        var capability = NavalLabCapabilityPrefix + nonce.ToString("N") + "." + runToken;
        if (NavalLabCapability != null && NavalLabCapability != capability)
            throw new InvalidOperationException("The process already belongs to another naval lab run.");
        NavalLabCapability = capability;
    }
#endif

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
