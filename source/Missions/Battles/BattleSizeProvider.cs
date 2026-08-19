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

        int configuredBattleSize;
        if (mapEvent.IsSiegeAmbush)
            configuredBattleSize = BannerlordConfig.GetRealBattleSizeForSallyOut();
        else if (mapEvent.IsSiegeAssault)
            configuredBattleSize = BannerlordConfig.GetRealBattleSizeForSiege();
        else
            configuredBattleSize = BannerlordConfig.GetRealBattleSize();

        return Math.Min(configuredBattleSize, DefaultBattleMissionAgentSpawnLogic.MaxNumberOfTroopsForMission);
    }
}
