using GameInterface.Services.BloodFeuds.Data;
using ProtoBuf;
using System.Collections.Generic;

namespace GameInterface.Services.BloodFeuds;

/// <summary>
/// ExecutionCampaignBehavior and the Clan blood feud fields track blood feuds for only one player clan
/// </summary>
[ProtoContract(SkipConstructor = true)]
public class BloodFeudPlayerData
{
    // Dictionary<PlayerHeroId, Dictionary<ClanId, BloodFeudData>> for Clan._hasBloodFeudWithPlayer and its execution counts
    [ProtoMember(1)]
    public Dictionary<string, Dictionary<string, BloodFeudData>> PlayerBloodFeuds { get; }

    // Dictionary<PlayerHeroId, Dictionary<ClanMemberHeroId, PendingSettlementExecution>> for _pendingClanMemberSettlementExecutions
    [ProtoMember(2)]
    public Dictionary<string, Dictionary<string, PendingSettlementExecution>> PlayerPendingSettlementExecutions { get; }

    // Dictionary<PlayerHeroId, PrisonerHeroIds> for _heroesPendingMapEventEndToBeExecuted
    [ProtoMember(3)]
    public Dictionary<string, List<string>> PlayerHeroesPendingMapEventEndExecution { get; }

    // Dictionary<PlayerHeroId, ExecutorHeroId> for _isMainHeroExecuted
    [ProtoMember(4)]
    public Dictionary<string, string> PlayerPendingExecutions { get; }

    public BloodFeudPlayerData(
        Dictionary<string, Dictionary<string, BloodFeudData>> playerBloodFeuds,
        Dictionary<string, Dictionary<string, PendingSettlementExecution>> playerPendingSettlementExecutions,
        Dictionary<string, List<string>> playerHeroesPendingMapEventEndExecution,
        Dictionary<string, string> playerPendingExecutions)
    {
        PlayerBloodFeuds = playerBloodFeuds ?? new();
        PlayerPendingSettlementExecutions = playerPendingSettlementExecutions ?? new();
        PlayerHeroesPendingMapEventEndExecution = playerHeroesPendingMapEventEndExecution ?? new();
        PlayerPendingExecutions = playerPendingExecutions ?? new();
    }
}
