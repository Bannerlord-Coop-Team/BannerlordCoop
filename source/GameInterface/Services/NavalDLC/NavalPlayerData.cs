using ProtoBuf;
using System.Collections.Generic;

namespace GameInterface.Services.NavalDLC;

/// <summary>
/// Some data structures used by the NavalDLC need to live inside the CoopSession for client specific data
/// UnlockedFigureheadsByMainHero assumes there is only the one player who can unlock figureheads
/// NavalDLCFigureheadCampaignBehavior._lastFigureheadLootTime assumes only one player loots figureheads
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

    public NavalPlayerData(
        Dictionary<string, List<string>> playerUnlockedFigureHeads,
        Dictionary<string, long> playerLastFigureheadLootTimes)
    {
        PlayerUnlockedFigureHeads = playerUnlockedFigureHeads ?? new();
        PlayerLastFigureheadLootTimes = playerLastFigureheadLootTimes ?? new();
    }
}
