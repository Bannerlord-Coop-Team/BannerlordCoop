using System;
using System.Net;

namespace Common.Network.Session;

/// <summary>Creates provider-owned pairwise tunnels while Missions sees loopback UDP endpoints.</summary>
public interface IMissionPeerTransport : IDisposable
{
    PlatformIdentity LocalIdentity { get; }
    event Action<PlatformIdentity> PeerDisconnected;

    void Start(int missionPort);
    bool TryConnect(PlatformIdentity remoteIdentity, out IPEndPoint endpoint);
    bool TryGetRemoteIdentity(IPEndPoint endpoint, out PlatformIdentity remoteIdentity);
    void Disconnect(PlatformIdentity remoteIdentity);
    void Stop();
}

/// <summary>Direct/server-relay fallback used when no platform peer transport is available.</summary>
public class NoopMissionPeerTransport : IMissionPeerTransport
{
    public PlatformIdentity LocalIdentity => default;

    public event Action<PlatformIdentity> PeerDisconnected
    {
        add { }
        remove { }
    }

    public void Start(int missionPort) { }

    public bool TryConnect(PlatformIdentity remoteIdentity, out IPEndPoint endpoint)
    {
        endpoint = null;
        return false;
    }

    public bool TryGetRemoteIdentity(IPEndPoint endpoint, out PlatformIdentity remoteIdentity)
    {
        remoteIdentity = default;
        return false;
    }

    public void Disconnect(PlatformIdentity remoteIdentity) { }
    public void Stop() { }
    public void Dispose() { }
}

/// <summary>The deterministic connection role one peer takes for a pairwise platform link.</summary>
public enum MissionPeerRole
{
    Unavailable,
    Listen,
    Connect,
}

/// <summary>Chooses one initiator so peers never create duplicate pairwise tunnels.</summary>
public static class MissionPeerRoles
{
    public static MissionPeerRole Resolve(PlatformIdentity localIdentity, PlatformIdentity remoteIdentity)
    {
        if (!localIdentity.IsValid || !remoteIdentity.IsValid || localIdentity == remoteIdentity)
            return MissionPeerRole.Unavailable;

        int providerComparison = string.CompareOrdinal(localIdentity.Provider, remoteIdentity.Provider);
        if (providerComparison != 0)
            return MissionPeerRole.Unavailable;

        return string.CompareOrdinal(localIdentity.UserId, remoteIdentity.UserId) < 0
            ? MissionPeerRole.Listen
            : MissionPeerRole.Connect;
    }
}
