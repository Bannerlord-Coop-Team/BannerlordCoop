using GameInterface.AutoSync;
using HarmonyLib;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.MapEventParties
{
    internal class MapEventPartySync : IAutoSync
    {
        public MapEventPartySync(AutoSyncRegistry autoSyncBuilder)
        {
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MapEventParty), nameof(MapEventParty.GainedRenownExplained)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MapEventParty), nameof(MapEventParty.GainedInfluenceExplained)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MapEventParty), nameof(MapEventParty.GainedMoraleExplained)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MapEventParty), nameof(MapEventParty.GoldLost)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MapEventParty), nameof(MapEventParty.Party)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MapEventParty), nameof(MapEventParty.PlunderedGold)));

            // Server-accumulated from synced troop score hits (OnTroopScoreHitAttempted → NetworkTroopScoreHit);
            // the clients' post-battle loot/captor models read it, so broadcast the server's value.
            autoSyncBuilder.AddField(AccessTools.Field(typeof(MapEventParty), nameof(MapEventParty._contributionToBattle)));
            autoSyncBuilder.AddField(AccessTools.Field(typeof(MapEventParty), nameof(MapEventParty._healthyManCountAtStart)));

            autoSyncBuilder.AddField(AccessTools.Field(typeof(MapEventParty), nameof(MapEventParty._diedInBattle)));
            autoSyncBuilder.AddField(AccessTools.Field(typeof(MapEventParty), nameof(MapEventParty._woundedInBattle)));
            autoSyncBuilder.AddField(AccessTools.Field(typeof(MapEventParty), nameof(MapEventParty._routedInBattle)));
        }
    }
}
