using GameInterface.Services.BloodFeuds.Data;
using ProtoBuf;
using System.Collections.Generic;

namespace GameInterface.Services.BloodFeuds;

/// <summary>
/// ExecutionCampaignBehavior and the Clan blood feud fields track blood feuds for only one player clan,
/// so every player clan gets its own copy and players in the same clan share it
/// </summary>
[ProtoContract(SkipConstructor = true)]
public class BloodFeudPlayerData
{
    // Dictionary<PlayerClanId, Dictionary<ClanId, BloodFeudData>> for Clan._hasBloodFeudWithPlayer and its execution counts
    [ProtoMember(1)]
    public Dictionary<string, Dictionary<string, BloodFeudData>> PlayerClanBloodFeuds { get; }

    // Dictionary<PlayerClanId, Dictionary<ClanMemberHeroId, PendingSettlementExecution>> for _pendingClanMemberSettlementExecutions
    [ProtoMember(2)]
    public Dictionary<string, Dictionary<string, PendingSettlementExecution>> PlayerClanPendingSettlementExecutions { get; }

    // Dictionary<PlayerClanId, PrisonerHeroIds> for _heroesPendingMapEventEndToBeExecuted
    [ProtoMember(3)]
    public Dictionary<string, List<string>> PlayerClanHeroesPendingMapEventEndExecution { get; }

    public BloodFeudPlayerData(
        Dictionary<string, Dictionary<string, BloodFeudData>> playerClanBloodFeuds,
        Dictionary<string, Dictionary<string, PendingSettlementExecution>> playerClanPendingSettlementExecutions,
        Dictionary<string, List<string>> playerClanHeroesPendingMapEventEndExecution)
    {
        PlayerClanBloodFeuds = playerClanBloodFeuds ?? new();
        PlayerClanPendingSettlementExecutions = playerClanPendingSettlementExecutions ?? new();
        PlayerClanHeroesPendingMapEventEndExecution = playerClanHeroesPendingMapEventEndExecution ?? new();
    }
}
