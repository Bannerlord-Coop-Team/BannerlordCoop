using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.MobileParties.Patches;

[HarmonyPatch(typeof(CampaignObjectManager))]
internal class MobilePartyDebugPatches
{
    [HarmonyPatch(nameof(CampaignObjectManager.AddMobileParty))]
    static bool Prefix(CampaignObjectManager __instance, MobileParty party)
    {
        party.Id = new MBGUID(14U, Campaign.Current.CampaignObjectManager.GetNextUniqueObjectIdOfType<MobileParty>());
        __instance._mobileParties.Add(party);
        __instance.OnItemAdded<MobileParty>(CampaignObjectManager.CampaignObjects.MobileParty, party);
        __instance.AddPartyToAppropriateList(party);

        return false;
    }
}
