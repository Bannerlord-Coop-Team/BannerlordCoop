using Common.Messaging;
using GameInterface.Services.Clans.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement;
using TaleWorlds.Core.ViewModelCollection.Selector;

namespace GameInterface.Services.Clans.Patches;

[HarmonyPatch(typeof(ClanPartyItemVM))]
internal class ClanPartyItemVMPatches
{
    [HarmonyPatch(nameof(ClanPartyItemVM.UpdatePartyBehaviorSelectionUpdate))]
    [HarmonyPrefix]
    public static bool UpdatePartyBehaviorSelectionUpdatePrefix(ref ClanPartyItemVM __instance, SelectorVM<SelectorItemVM> s)
    {
        if (s.SelectedIndex != (int)__instance.Party.MobileParty.Objective)
        {
            // Manage setting the party behavior on the server
            var message = new PartyBehaviorUpdatedOnSelection(__instance.Party.MobileParty, (MobileParty.PartyObjective)s.SelectedIndex);
            MessageBroker.Instance.Publish(__instance, message);
        }

        return false;
    }
    
    [HarmonyPatch(nameof(ClanPartyItemVM.OnAutoRecruitChanged))]
    [HarmonyPrefix]
    public static bool OnAutoRecruitChangedPrefix(ref ClanPartyItemVM __instance, bool value)
    {
        if (__instance.Party.IsMobile && __instance.Party.MobileParty.IsGarrison)
        {
            Settlement homeSettlement = __instance.Party.MobileParty.HomeSettlement;
            if (homeSettlement?.Town != null)
            {
                // Manage setting auto recruitment on the server
                var message = new AutoRecruitChangedForSettlement(__instance.Party.MobileParty.HomeSettlement, value);
                MessageBroker.Instance.Publish(__instance, message);
            }
        }

        return false;
    }

}
