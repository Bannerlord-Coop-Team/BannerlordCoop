using System;
using System.Collections.Generic;
using System.Text;
using GameInterface.AutoSync;
using HarmonyLib;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.MapEventParties
{
    internal class MapEventPartySync : IAutoSync
    {
        public MapEventPartySync(IAutoSyncBuilder autoSyncBuilder)
        {
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MapEventParty), nameof(MapEventParty.GainedInfluence)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MapEventParty), nameof(MapEventParty.GainedRenown)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MapEventParty), nameof(MapEventParty.GoldLost)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MapEventParty), nameof(MapEventParty.MoraleChange)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MapEventParty), nameof(MapEventParty.Party)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MapEventParty), nameof(MapEventParty.PlunderedGold)));
        
            
        }
    }
}
