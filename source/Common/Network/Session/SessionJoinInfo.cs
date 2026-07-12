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

    /// <summary>Steam id of the player hosting the tunnel; 0 when the session is direct-only.</summary>
    public ulong HostSteamId { get; set; }

    /// <summary>
    /// Game-server Steam id of a standalone server that listens on Steam itself; 0 for a
    /// player-hosted session. When set, a joiner tunnels to this identity instead of the lobby
    /// owner, so the owner (a connected player, or none) is not the connect target.
    /// </summary>
    public ulong ServerSteamId { get; set; }

    /// <summary>The host's mod build string, shown to a joiner so they can compare versions.</summary>
    public string ModVersion { get; set; }

    /// <summary>True when the server requires a password before admitting the connection.</summary>
    public bool PasswordRequired { get; set; }

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
