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

    /*
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
    */

    /*
    [HarmonyPatch(nameof(ClanPartyItemVM.OnFinalize))]
    [HarmonyPostfix]
    public static void OnFinalizePostfix(ref ClanPartyItemVM __instance)
    {
        var partyComponent = __instance.ExpenseItem._mobileParty._partyComponent;
        int newLimit = 0;
        if (partyComponent is LordPartyComponent)
        {
            newLimit = partyComponent.WagePaymentLimit;
        }
        else if (partyComponent is GarrisonPartyComponent component)
        {
            newLimit = component.Settlement.GarrisonWagePaymentLimit;
        }

        // Send new party wage limit settings to server to sync
        // Do this as part of the finalization to avoid syncing the slider every time it changes
        var message = new ClanPartyItemVMFinalized(partyComponent, newLimit);
        MessageBroker.Instance.Publish(__instance, message);
    }
    */
}
