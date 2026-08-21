using System;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.MountAndBlade;

namespace Missions.Battles;

public interface IBattleSizeProvider
{
    int GetBattleSize(MapEvent mapEvent);
}

public class BattleSizeProvider : IBattleSizeProvider
{
    public int GetBattleSize(MapEvent mapEvent)
    {
        if (mapEvent == null) throw new ArgumentNullException(nameof(mapEvent));

        int configuredBattleSize = mapEvent.IsSiegeAssault
            ? BannerlordConfig.GetRealBattleSizeForSiege()
            : BannerlordConfig.GetRealBattleSize();
        return Math.Min(configuredBattleSize, DefaultBattleMissionAgentSpawnLogic.MaxNumberOfTroopsForMission);
    }
}
