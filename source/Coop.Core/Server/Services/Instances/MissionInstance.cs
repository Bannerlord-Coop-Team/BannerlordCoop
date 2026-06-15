using LiteNetLib;
using System;
using System.Collections.Generic;
using System.Net;

namespace Coop.Core.Server.Services.Instances;

/// <summary>
/// Server-side record of a single P2P mission instance: the group of co-located players sharing one
/// settlement interior, keyed by settlement + location. Tracks campaign-peer membership (for host
/// election and instance assignment) and the P2P socket endpoints each member presents (for NAT
/// introduction). Not serialized — only the <see cref="Id"/> crosses the wire (see
/// <see cref="Messages.NetworkAssignInstance"/>).
/// </summary>
internal class MissionInstance
{
    public Guid Id { get; }
    public string SettlementId { get; }
    public string LocationId { get; }

    /// <summary>The peer that owns NPC simulation inside the scene. Never null while members exist.</summary>
    public NetPeer Host { get; set; }

    public List<NetPeer> Members { get; } = new List<NetPeer>();

    /// <summary>
    /// P2P socket endpoints presented via NAT-introduction requests. These arrive on a DIFFERENT
    /// socket than the campaign <see cref="Members"/> connections, so they are tracked separately and
    /// cannot be correlated back to a member by endpoint. Cleared when the instance is retired.
    /// </summary>
    public List<Endpoints> PunchEndpoints { get; } = new List<Endpoints>();

    public MissionInstance(Guid id, string settlementId, string locationId)
    {
        Id = id;
        SettlementId = settlementId;
        LocationId = locationId;
    }

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
