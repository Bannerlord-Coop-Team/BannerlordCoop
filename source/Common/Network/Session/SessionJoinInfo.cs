namespace Common.Network.Session;

/// <summary>
/// What a joiner needs to reach a running session. Advertised through an
/// <see cref="ISessionAdvertiser"/> and consumed by the join flow.
/// </summary>
public class SessionJoinInfo
{
    // Bumped when tunnel identities became provider-scoped.
    public const int CurrentVersion = 4;

    public int Version { get; set; } = CurrentVersion;
    public string Address { get; set; }
    public int Port { get; set; }

    /// <summary>Provider-owned peer reached by the session tunnel.</summary>
    public PlatformIdentity TunnelTarget { get; set; }

    /// <summary>Whether the advertisement belongs to a standalone server.</summary>
    public bool DedicatedServer { get; set; }

    /// <summary>The host's exact mod build, displayed and checked before a provider join.</summary>
    public string ModVersion { get; set; }

    /// <summary>True when the server requires a password before admitting the connection.</summary>
    public bool PasswordRequired { get; set; }

    /// <summary>Players currently connected to the standalone server.</summary>
    public int ConnectedPlayers { get; set; }

    /// <summary>
    /// Whether this standalone session should appear in the co-op server discovery UI. This does
    /// not disable its provider lobby, tunnel, rich presence, or direct join paths.
    /// </summary>
    public bool Discoverable { get; set; } = true;

    /// <summary>
    /// Password supplied locally by the joiner. This is transient join state and is never encoded
    /// into provider lobby data.
    /// </summary>
    public string Password { get; set; }

    /// <summary>Set on prepared join info when the returned endpoint is a local tunnel pump.</summary>
    public bool Tunneled { get; set; }

    public bool HasAddress => !string.IsNullOrEmpty(Address);

    public bool HasTunnelTarget => TunnelTarget.IsValid;
}
