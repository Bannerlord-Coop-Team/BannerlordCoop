using GameInterface.AutoSync;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Siege;

namespace GameInterface.Services.SiegeEngineMissiles
{
    internal class SiegeEngineMissileSync : IAutoSync
    {
        public SiegeEngineMissileSync(AutoSyncRegistry autoSyncBuilder)
        {
            // Fields
            autoSyncBuilder.AddField(AccessTools.Field(typeof(SiegeEvent.SiegeEngineMissile), nameof(SiegeEvent.SiegeEngineMissile.ShooterSiegeEngineType)));
            autoSyncBuilder.AddField(AccessTools.Field(typeof(SiegeEvent.SiegeEngineMissile), nameof(SiegeEvent.SiegeEngineMissile.ShooterSlotIndex)));
            autoSyncBuilder.AddField(AccessTools.Field(typeof(SiegeEvent.SiegeEngineMissile), nameof(SiegeEvent.SiegeEngineMissile.TargetType)));
            autoSyncBuilder.AddField(AccessTools.Field(typeof(SiegeEvent.SiegeEngineMissile), nameof(SiegeEvent.SiegeEngineMissile.TargetSlotIndex)));
            autoSyncBuilder.AddField(AccessTools.Field(typeof(SiegeEvent.SiegeEngineMissile), nameof(SiegeEvent.SiegeEngineMissile.TargetSiegeEngine)));
            autoSyncBuilder.AddField(AccessTools.Field(typeof(SiegeEvent.SiegeEngineMissile), nameof(SiegeEvent.SiegeEngineMissile.CollisionTime)));
            autoSyncBuilder.AddField(AccessTools.Field(typeof(SiegeEvent.SiegeEngineMissile), nameof(SiegeEvent.SiegeEngineMissile.FireDecisionTime)));
            autoSyncBuilder.AddField(AccessTools.Field(typeof(SiegeEvent.SiegeEngineMissile), nameof(SiegeEvent.SiegeEngineMissile.HitSuccessful)));
        }
    }
}
