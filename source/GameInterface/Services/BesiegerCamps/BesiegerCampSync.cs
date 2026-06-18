using GameInterface.AutoSync;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Siege;

namespace GameInterface.Services.BesiegerCamps
{
    internal class BesiegerCampSync : IAutoSync
    {
        public BesiegerCampSync(AutoSyncRegistry AutoSyncRegistry)
        {
            // Fields
            AutoSyncRegistry.AddField(AccessTools.Field(typeof(BesiegerCamp), nameof(BesiegerCamp._leaderParty)));  

            // Properties
            AutoSyncRegistry.AddProperty(AccessTools.Property(typeof(BesiegerCamp), nameof(BesiegerCamp.NumberOfTroopsKilledOnSide)));
            AutoSyncRegistry.AddProperty(AccessTools.Property(typeof(BesiegerCamp), nameof(BesiegerCamp.SiegeEvent)));
            AutoSyncRegistry.AddProperty(AccessTools.Property(typeof(BesiegerCamp), nameof(BesiegerCamp.SiegeEngines)));
            AutoSyncRegistry.AddProperty(AccessTools.Property(typeof(BesiegerCamp), nameof(BesiegerCamp.SiegeStrategy)));

            // Targetmethods
        }
    }
}