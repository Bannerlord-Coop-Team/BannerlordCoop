namespace Common.Network.Session;

/// <summary>
/// What a joiner needs to reach a running session. Advertised through an
/// <see cref="ISessionAdvertiser"/> and consumed by the join flow.
/// </summary>
public class SessionJoinInfo
{
    // Bumped to 3 when standalone servers began advertising their own game-server identity; a
    // lobby written by a newer mod fails an older client's decode with an "update your mod" message.
    public const int CurrentVersion = 3;

    /// <summary>
    /// Sessions advertised at or above this version have a Steam tunnel listening, so a
    /// joiner can connect to the lobby owner instead of dialing the address.
    /// </summary>
    public const int MinTunnelVersion = 2;

    public int Version { get; set; } = CurrentVersion;
    public string Address { get; set; }
    public int Port { get; set; }

    /// <summary>
    /// Resolved Steam identity of the tunnel endpoint: the standalone server's
    /// <see cref="ServerSteamId"/>, or the lobby owner's user Steam id for a client-owned lobby.
    /// Zero for a direct-only session.
    /// </summary>
    public ulong HostSteamId { get; set; }

    /// <summary>
    /// Steam game-server id advertised by a standalone server. Zero for a client-owned lobby;
    /// joiners then resolve <see cref="HostSteamId"/> from the lobby owner.
    /// </summary>
    public ulong ServerSteamId { get; set; }

    /// <summary>The host's exact mod build, displayed and checked before a Steam join.</summary>
    public string ModVersion { get; set; }

    /// <summary>True when the server requires a password before admitting the connection.</summary>
    public bool PasswordRequired { get; set; }

    /// <summary>Players currently connected to the standalone server.</summary>
    public int ConnectedPlayers { get; set; }

    /// <summary>
    /// Whether this standalone session should appear in the co-op server discovery UI. This does
    /// not disable its Steam lobby, tunnel, rich presence, or direct join paths.
    /// </summary>
    public bool Discoverable { get; set; } = true;

    /// <summary>
    /// Password supplied locally by the joiner. This is transient join state and is never encoded
    /// into Steam lobby data.
    /// </summary>
    public string Password { get; set; }

    /// <summary>Set on prepared join info when the returned endpoint is a local tunnel pump.</summary>
    public bool Tunneled { get; set; }

    public bool HasAddress => !string.IsNullOrEmpty(Address);

    public bool HasHostSteamId => HostSteamId != 0;

    public bool HasServerSteamId => ServerSteamId != 0;
}
