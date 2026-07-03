namespace Common.Network.Session;

/// <summary>
/// What a joiner needs to reach a running session. Advertised through an
/// <see cref="ISessionAdvertiser"/> and consumed by the join flow.
/// </summary>
public class SessionJoinInfo
{
    public const int CurrentVersion = 2;

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

    /// <summary>Set on prepared join info when the returned endpoint is a local tunnel pump.</summary>
    public bool Tunneled { get; set; }

    public bool HasAddress => !string.IsNullOrEmpty(Address);

    public bool HasHostSteamId => HostSteamId != 0;
}
