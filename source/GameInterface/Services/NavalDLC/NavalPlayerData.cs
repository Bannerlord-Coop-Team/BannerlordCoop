using ProtoBuf;
using System.Collections.Generic;

namespace GameInterface.Services.NavalDLC;

/// <summary>
/// Some data structures used by the NavalDLC need to live inside the CoopSession for client specific data
/// UnlockedFigureheadsByMainHero assumes there is only the one player who can unlock figureheads
/// NavalDLCFigureheadCampaignBehavior._lastFigureheadLootTime assumes only one player loots figureheads
/// ClanFleetManagementCampaignBehavior._sentTroops assumes only one player sends troops away with ships
/// RaftStateCampaignBehavior._playerCachedShips and _cachedAnchorPoint assumes only one player has cached ships that can be restored
/// </summary>
[ProtoContract(SkipConstructor = true)]
public class NavalPlayerData
{
    // Dictionary<PlayerHeroId, List<FigureheadId>>
    [ProtoMember(1)]
    public Dictionary<string, List<string>> PlayerUnlockedFigureHeads { get; }

    // Dictionary<PlayerHeroId, CampaignTime._numTicks>
    [ProtoMember(2)]
    public Dictionary<string, long> PlayerLastFigureheadLootTimes { get; }

    // Dictionary<PlayerHeroId, List<SentTroopsData>>
    [ProtoMember(3)]
    public Dictionary<string, List<SentTroopsData>> PlayerSentTroops { get; }

    // Dictionary<PlayerHeroId, PlayerCachedFleetData>
    [ProtoMember(4)]
    public Dictionary<string, PlayerCachedFleetData> PlayerCachedFleets { get; }

    public NavalPlayerData(
        Dictionary<string, List<string>> playerUnlockedFigureHeads,
        Dictionary<string, long> playerLastFigureheadLootTimes,
        Dictionary<string, List<SentTroopsData>> playerSentTroops,
        Dictionary<string, PlayerCachedFleetData> playerCachedFleets)
    {
        PlayerUnlockedFigureHeads = playerUnlockedFigureHeads ?? new();
        PlayerLastFigureheadLootTimes = playerLastFigureheadLootTimes ?? new();
        PlayerSentTroops = playerSentTroops ?? new();
        PlayerCachedFleets = playerCachedFleets ?? new();
    }
}
