using GameInterface.AutoSync;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.MobileParties;
internal class MobilePartySync : IAutoSync
{
    public MobilePartySync(IAutoSyncBuilder autoSyncBuilder)
    {
        // Sync a property
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MobileParty), nameof(MobileParty.Ai)));
    }
}
