using GameInterface.AutoSync;
using GameInterface.DynamicSync;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;

namespace GameInterface.Services.SiegeEngineMissiles
{
    internal class SiegeEngineMissileSync : IDynamicSync
    {
        public SiegeEngineMissileSync(DynamicSyncRegistry autoSyncBuilder)
        {
            //autoSyncBuilder.AddField(AccessTools.Field(typeof(SiegeEvent.SiegeEngineMissile), nameof(SiegeEvent.SiegeEngineMissile.ShooterSiegeEngineType)));
            //autoSyncBuilder.AddField(AccessTools.Field(typeof(SiegeEvent.SiegeEngineMissile), nameof(SiegeEvent.SiegeEngineMissile.ShooterSlotIndex)));
        }
    }
}
