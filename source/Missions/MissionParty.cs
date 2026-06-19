using System;
using System.Collections.Generic;

namespace GameInterface.Missions;

/// <summary>
/// A networked party within a mission: a leader agent plus its troops, owned by a controller.
/// </summary>
/// <remarks>
/// <see cref="OriginalOwner"/> identifies the controller the party belongs to and never changes — it is
/// what lets a rejoining client reclaim its party. <see cref="CurrentAuthority"/> is who simulates the
/// party right now: the owner while it is connected, the host while the owner is disconnected. The two are
/// equal for a host-owned NPC party, which therefore never transfers.
/// <para>All mutation goes through <see cref="MissionPartyRegistry"/> under its lock.</para>
/// </remarks>
public class MissionParty
{
    public Guid PartyId { get; }
    public string OriginalOwner { get; }
    public Guid LeaderAgentId { get; }

    /// <summary>Controller currently driving this party. Mutated only by <see cref="MissionPartyRegistry"/>.</summary>
    public string CurrentAuthority { get; internal set; }

    private readonly HashSet<Guid> troopAgentIds;
    public IReadOnlyCollection<Guid> TroopAgentIds => troopAgentIds;

    internal MissionParty(Guid partyId, string originalOwner, Guid leaderAgentId, IEnumerable<Guid> troopAgentIds)
    {
        PartyId = partyId;
        OriginalOwner = originalOwner;
        LeaderAgentId = leaderAgentId;
        CurrentAuthority = originalOwner;
        this.troopAgentIds = troopAgentIds is null ? new HashSet<Guid>() : new HashSet<Guid>(troopAgentIds);
    }

    /// <summary>The leader id followed by every troop id.</summary>
    public IEnumerable<Guid> AllAgentIds
    {
        get
        {
            yield return LeaderAgentId;
            foreach (var troop in troopAgentIds)
                yield return troop;
        }
    }

    internal bool AddTroop(Guid agentId) => troopAgentIds.Add(agentId);
    internal bool RemoveTroop(Guid agentId) => troopAgentIds.Remove(agentId);
}
