using System;
using System.Reflection;

namespace Common;

public static class ModInformation
{
    public static bool IsServer { get; set; } = false;
    public static bool IsClient => !IsServer;

    public static Version Version => new("0.0.1");

    /// <summary>
    /// The mod's build string (the informational version stamped on this assembly), shown so a
    /// joiner can compare it against a lobby's advertised version before joining.
    /// </summary>
    public static string BuildVersion { get; } = typeof(ModInformation).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
        ?.InformationalVersion ?? "unknown";
}
