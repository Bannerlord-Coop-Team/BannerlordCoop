using System;
using System.Net;

namespace Common.Network.Session;

/// <summary>
/// Creates pairwise Steam tunnels for mission traffic while Missions remains independent of
/// Steamworks and continues to see ordinary loopback UDP endpoints.
/// </summary>
public interface ISteamMissionBridge : IDisposable
{
    ulong LocalSteamId { get; }
    event Action<ulong> PeerDisconnected;

    void Start(int missionPort);
    bool TryConnect(ulong remoteSteamId, out IPEndPoint endpoint);
    bool TryGetRemoteSteamId(IPEndPoint endpoint, out ulong remoteSteamId);
    void Disconnect(ulong remoteSteamId);
    void Stop();
}

/// <summary>Direct-IP fallback used when Steam integration is unavailable.</summary>
public class NoopSteamMissionBridge : ISteamMissionBridge
{
    public ulong LocalSteamId => 0;

    public event Action<ulong> PeerDisconnected
    {
        add { }
        remove { }
    }

    public void Start(int missionPort) { }

    public bool TryConnect(ulong remoteSteamId, out IPEndPoint endpoint)
    {
        endpoint = null;
        return false;
    }

    public bool TryGetRemoteSteamId(IPEndPoint endpoint, out ulong remoteSteamId)
    {
        remoteSteamId = 0;
        return false;
    }

    public void Disconnect(ulong remoteSteamId) { }
    public void Stop() { }
    public void Dispose() { }
}
