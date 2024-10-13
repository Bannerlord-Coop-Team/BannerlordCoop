using GameInterface.AutoSync;
using HarmonyLib;
using Helpers;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;

namespace GameInterface.Services.Sieges
{
    internal class SiegeEventSync : IAutoSync
    {
        public SiegeEventSync(IAutoSyncBuilder autoSyncBuilder)
        {
            // Fields
            autoSyncBuilder.AddField(AccessTools.Field(typeof(SiegeEvent), nameof(SiegeEvent.BesiegedSettlement)));
            autoSyncBuilder.AddField(AccessTools.Field(typeof(SiegeEvent), nameof(SiegeEvent.BesiegerCamp)));
            autoSyncBuilder.AddField(AccessTools.Field(typeof(SiegeEvent), nameof(SiegeEvent._isBesiegerDefeated)));

            // Props
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(SiegeEvent), nameof(SiegeEvent.SiegeStartTime)));
        }
    }
}
