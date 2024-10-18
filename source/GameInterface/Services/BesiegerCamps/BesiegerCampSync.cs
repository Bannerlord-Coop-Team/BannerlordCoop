using GameInterface.AutoSync;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Siege;

namespace GameInterface.Services.BesiegerCamps
{
    internal class BesiegerCampSync : IAutoSync
    {
        public BesiegerCampSync(IAutoSyncBuilder autoSyncBuilder)
        {
            // Fields
            autoSyncBuilder.AddField(AccessTools.Field(typeof(BesiegerCamp), nameof(BesiegerCamp._leaderParty)));

            // Props
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(BesiegerCamp), nameof(BesiegerCamp.NumberOfTroopsKilledOnSide)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(BesiegerCamp), nameof(BesiegerCamp.SiegeEvent)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(BesiegerCamp), nameof(BesiegerCamp.SiegeEngines)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(BesiegerCamp), nameof(BesiegerCamp.SiegeStrategy)));
        }
    }
}