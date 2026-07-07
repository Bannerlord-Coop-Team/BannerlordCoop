using GameInterface.AutoSync;
using HarmonyLib;
using static TaleWorlds.CampaignSystem.Siege.SiegeEvent;

namespace GameInterface.Services.SiegeEnginesConstructionProgress
{
    internal class SiegeEngineConstructionProgressSync : IAutoSync
    {
        public SiegeEngineConstructionProgressSync(AutoSyncRegistry autoSyncBuilder)
        {
            // Props
            // Progress and RedeploymentProgress mutate every campaign tick, so they use the threshold
            // sync in SiegeEngineProgressPatches instead of a per-set property sync. RangedSiegeEngine
            // is the server-only bombardment state and is never read on clients. The readonly
            // SiegeEngine field cannot reference-sync (catalog SiegeEngineTypes are XML objects the
            // co-op registry never holds); the deploy/reserve messages carry the type id instead and
            // the client apply fills the shell.
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(SiegeEngineConstructionProgress), nameof(SiegeEngineConstructionProgress.Hitpoints)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(SiegeEngineConstructionProgress), nameof(SiegeEngineConstructionProgress.MaxHitPoints)));
        }
    }
}
