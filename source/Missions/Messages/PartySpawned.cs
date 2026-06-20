using Common.Messaging;
using ProtoBuf;
using System;

namespace GameInterface.Missions.Messages;

/// <summary>
/// Announces a spawned party so every node registers it with the same id and owner. A client broadcasts it
/// for its own party (<see cref="OriginalOwner"/> = the client); the host broadcasts it for each NPC party
/// (<see cref="OriginalOwner"/> = the host). The agent ids are the stable network ids; the engine spawn data
/// (character/equipment/position) is carried separately by the spawning controller.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public class PartySpawned : IEvent
{
    [ProtoMember(1)]
    public readonly Guid PartyId;

    [ProtoMember(2)]
    public readonly string OriginalOwner;

    [ProtoMember(3)]
    public readonly Guid LeaderAgentId;

    [ProtoMember(4)]
    public readonly Guid[] TroopAgentIds;

    public PartySpawned(Guid partyId, string originalOwner, Guid leaderAgentId, Guid[] troopAgentIds)
    {
        PartyId = partyId;
        OriginalOwner = originalOwner;
        LeaderAgentId = leaderAgentId;
        TroopAgentIds = troopAgentIds ?? Array.Empty<Guid>();
    }
}
