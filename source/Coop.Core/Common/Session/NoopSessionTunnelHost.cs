using Common.Network.Session;

namespace Coop.Core.Common.Session;

/// <summary>
/// Tunnel host for sessions without a relay transport (no Steam): direct-IP joiners dial
/// the server themselves.
/// </summary>
public class NoopSessionTunnelHost : ISessionTunnelHost
{
    public bool IsListening => false;

    public int PeerCount => 0;

    public void Start(int serverPort)
    {
    }

    public void Stop()
    {
    }

    public void Dispose()
    {
    }
}
