using LiteNetLib;
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
    /// P2P socket endpoints presented via NAT-introduction requests, keyed by controller and campaign connection.
    /// </summary>
    public List<Endpoints> PunchEndpoints { get; } = new List<Endpoints>();

    internal HashSet<MissionMembership> Memberships { get; } = new HashSet<MissionMembership>();

    public MissionInstance(string id)
    {
        Id = id;
    }

    /// <summary>Controller ids currently routed through this instance (relay-fallback membership).</summary>
    public IReadOnlyCollection<string> Controllers => Memberships.Select(member => member.ControllerId).ToArray();

    /// <summary>The campaign connection and socket endpoints presented for NAT introduction.</summary>
    public readonly struct Endpoints
    {
        public readonly string ControllerId;
        public readonly NetPeer CampaignPeer;
        public readonly IPEndPoint Internal;
        public readonly IPEndPoint External;

        public Endpoints(
            string controllerId,
            NetPeer campaignPeer,
            IPEndPoint @internal,
            IPEndPoint external)
        {
            ControllerId = controllerId;
            CampaignPeer = campaignPeer;
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

    public MissionMembership(string controllerId, NetPeer peer, MissionInstance instance)
    {
        ControllerId = controllerId;
        Peer = peer;
        Instance = instance;
    }
}
