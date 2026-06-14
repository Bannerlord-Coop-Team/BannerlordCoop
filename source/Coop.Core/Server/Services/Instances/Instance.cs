using LiteNetLib;
using System;
using System.Collections.Generic;

namespace Coop.Core.Server.Services.Instances;

/// <summary>
/// Server-side record of a single P2P instance: the group of co-located players sharing one
/// settlement interior (keyed by settlement + location). Not serialized — only the
/// <see cref="Id"/> crosses the wire (see <see cref="Messages.NetworkAssignInstance"/>).
/// </summary>
internal class Instance
{
    public Guid Id { get; }
    public string SettlementId { get; }
    public string LocationId { get; }

    /// <summary>The peer that owns NPC simulation inside the scene. Never null while members exist.</summary>
    public NetPeer Host { get; set; }

    public List<NetPeer> Members { get; } = new List<NetPeer>();

    public Instance(Guid id, string settlementId, string locationId)
    {
        Id = id;
        SettlementId = settlementId;
        LocationId = locationId;
    }
}
