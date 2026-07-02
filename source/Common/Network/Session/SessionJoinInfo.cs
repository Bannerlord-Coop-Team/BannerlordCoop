namespace Common.Network.Session;

/// <summary>
/// What a joiner needs to reach a running session. Advertised through an
/// <see cref="ISessionAdvertiser"/> and consumed by the join flow.
/// </summary>
public class SessionJoinInfo
{
    public const int CurrentVersion = 1;

    public int Version { get; set; } = CurrentVersion;
    public string Address { get; set; }
    public int Port { get; set; }

    public bool HasAddress => !string.IsNullOrEmpty(Address);
}
