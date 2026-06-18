using GameInterface.AutoSync;
using GameInterface.AutoSync;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;

namespace GameInterface.Services.SiegeEngineMissiles
{
    internal class SiegeEngineMissileSync : IAutoSync
    {
        public SiegeEngineMissileSync(AutoSyncRegistry autoSyncBuilder)
        {
            //autoSyncBuilder.AddField(AccessTools.Field(typeof(SiegeEvent.SiegeEngineMissile), nameof(SiegeEvent.SiegeEngineMissile.ShooterSiegeEngineType)));
            //autoSyncBuilder.AddField(AccessTools.Field(typeof(SiegeEvent.SiegeEngineMissile), nameof(SiegeEvent.SiegeEngineMissile.ShooterSlotIndex)));
        }
    }
}
