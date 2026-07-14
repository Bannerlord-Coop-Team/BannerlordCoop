using Common.Network.Session;
using System.Net;

namespace Coop.Core.Common.Session;

/// <summary>
/// Tunnel host for sessions without a relay transport (no Steam): direct-IP joiners dial
/// the server themselves.
/// </summary>
public class NoopSessionTunnelHost : ISessionTunnelHost, ISessionTunnelIdentityResolver
{
    public bool IsListening => false;

    public int PeerCount => 0;

    public void Start(int serverPort)
    {
    }

    public bool TryGetRemoteSteamId(IPEndPoint serverPeerEndpoint, out ulong steamId)
    {
        steamId = 0;
        return false;
    }

    public void Stop()
    {
    }

    public void Dispose()
    {
    }
}
