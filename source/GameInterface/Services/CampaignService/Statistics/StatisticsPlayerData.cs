using GameInterface.Services.CampaignService.Statistics.Data;
using ProtoBuf;
using System.Collections.Generic;

namespace GameInterface.Services.CampaignService.Statistics;

/// <summary>
/// StatisticsCampaignBehavior assumes there is only ever one player.
/// This stores each player's statistics mapped to their current hero, preserved during migration.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public class StatisticsPlayerData
{
    // Dictionary<PlayerHeroId, StatisticsData>
    [ProtoMember(1)]
    public Dictionary<string, StatisticsData> PlayerStatistics { get; }

    public StatisticsPlayerData(Dictionary<string, StatisticsData> playerStatistics)
    {
        PlayerStatistics = playerStatistics ?? new();
    }
}
