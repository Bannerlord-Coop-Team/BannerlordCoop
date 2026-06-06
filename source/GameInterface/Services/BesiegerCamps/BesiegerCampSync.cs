using GameInterface.DynamicSync;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Siege;

namespace GameInterface.Services.BesiegerCamps
{
    internal class BesiegerCampSync : IDynamicSync
    {
        public BesiegerCampSync(DynamicSyncRegistry dynamicSyncRegistry)
        {
            // Fields
            dynamicSyncRegistry.AddField(AccessTools.Field(typeof(BesiegerCamp), nameof(BesiegerCamp._leaderParty)));  

            // Properties
            dynamicSyncRegistry.AddProperty(AccessTools.Property(typeof(BesiegerCamp), nameof(BesiegerCamp.NumberOfTroopsKilledOnSide)));
            dynamicSyncRegistry.AddProperty(AccessTools.Property(typeof(BesiegerCamp), nameof(BesiegerCamp.SiegeEvent)));
            dynamicSyncRegistry.AddProperty(AccessTools.Property(typeof(BesiegerCamp), nameof(BesiegerCamp.SiegeEngines)));
            dynamicSyncRegistry.AddProperty(AccessTools.Property(typeof(BesiegerCamp), nameof(BesiegerCamp.SiegeStrategy)));

            // Targetmethods
        }
    }
}