using LiteNetLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace Coop.Core.Server.Services.Instances;

/// <summary>
/// Server-side record of a single P2P mission instance: the group of co-located players sharing one
/// settlement interior. The id is derived client-side from settlement + location; the server holds the
/// P2P socket endpoints each peer presents for NAT introduction, and — for the relay fallback — the
/// mapping from each member's controller id to its live server-side connection.
/// </summary>
internal class MissionInstance
{
    public string Id { get; }

    /// <summary>
    /// P2P socket endpoints presented via NAT-introduction requests.
    /// </summary>
    public List<Endpoints> PunchEndpoints { get; } = new List<Endpoints>();

    internal HashSet<MissionMembership> Memberships { get; } = new HashSet<MissionMembership>();

    public MissionInstance(string id)
    {
        Id = id;
    }

    /// <summary>Controller ids currently routed through this instance (relay-fallback membership).</summary>
    public IReadOnlyCollection<string> Controllers => Memberships.Select(member => member.ControllerId).ToArray();

    /// <summary>The internal (LAN) and external (WAN) endpoints a peer presents for NAT introduction.</summary>
    public readonly struct Endpoints
    {
        public readonly IPEndPoint Internal;
        public readonly IPEndPoint External;

        public Endpoints(IPEndPoint @internal, IPEndPoint external)
        {
            Internal = @internal;
            External = external;
        }
    }
}

internal sealed class MissionMembership
{
    public string ControllerId { get; }
    public NetPeer Peer { get; set; }
    public MissionInstance Instance { get; }
    public Guid PeerCredential { get; set; }

    public MissionMembership(
        string controllerId,
        NetPeer peer,
        MissionInstance instance,
        Guid peerCredential)
    {
        ControllerId = controllerId;
        Peer = peer;
        Instance = instance;
        PeerCredential = peerCredential;
    }
}
