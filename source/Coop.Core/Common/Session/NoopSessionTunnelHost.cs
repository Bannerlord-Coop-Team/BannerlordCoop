using Common.Network.Session;
using System.Net;

namespace Coop.Core.Common.Session;

/// <summary>
/// Tunnel host for sessions without a provider relay: direct-IP joiners dial
/// the server themselves.
/// </summary>
public class NoopSessionTunnelHost : ISessionTunnelHost, IAuthenticatedPeerIdentityResolver
{
    public bool IsListening => false;

    public int PeerCount => 0;

    public void Start(int serverPort)
    {
    }

    public bool TryGetIdentity(IPEndPoint serverPeerEndpoint, out PlatformIdentity identity)
    {
        identity = default;
        return false;
    }

    public void Stop()
    {
    }

    public void Dispose()
    {
    }
}
