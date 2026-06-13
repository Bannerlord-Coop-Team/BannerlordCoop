using Common.Messaging;
using GameInterface.Services.Clans.Messages;
using HarmonyLib;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement.Categories;

namespace GameInterface.Services.Clans.Patches;

[HarmonyPatch(typeof(ClanPartiesVM))]
internal class ClanPartiesVMPatches
{
    [HarmonyPatch(nameof(ClanPartiesVM.CreateNewClanParty))]
    [HarmonyPrefix]
    public static bool CreateNewClanPartyPrefix(ref ClanPartiesVM __instance, Hero newLeader, int partyGoldLowerThreshold)
    {
        if (newLeader.PartyBelongedTo == MobileParty.MainParty)
        {
            __instance._openPartyAsManage(newLeader);
            return false;
        }

        // Create and manage the new mobile party on the server
        var message = new NewClanPartyCreated(Hero.MainHero, newLeader, __instance._faction, partyGoldLowerThreshold);
        MessageBroker.Instance.Publish(__instance, message);

        __instance._onRefresh();

        return false;
    }

    [HarmonyPatch(nameof(ClanPartiesVM.OnPartyLeaderChanged))]
    [HarmonyPrefix]
    public static bool OnPartyLeaderChangedPrefix(ref ClanPartiesVM __instance, Hero newLeader)
    {
        var selectedParty = __instance.CurrentSelectedParty.Party.MobileParty;
        var oldLeader = __instance.CurrentSelectedParty.Party.LeaderHero;

        // Change clan party leader on the server
        var message = new ClanPartyLeaderChanged(Hero.MainHero, newLeader, oldLeader, selectedParty, MobileParty.MainParty);
        MessageBroker.Instance.Publish(__instance, message);

        return false;
    }

    [HarmonyPatch(nameof(ClanPartiesVM.OnDisbandCurrentParty))]
    [HarmonyPrefix]
    public static bool OnDisbandCurrentPartyPrefix(ref ClanPartiesVM __instance)
    {
        // Disband clan party on the server
        var message = new ClanPartyDisbanded(__instance.CurrentSelectedParty.Party.MobileParty);
        MessageBroker.Instance.Publish(__instance, message);

        return false;
    }

    [HarmonyPatch(nameof(ClanPartiesVM.OnFinalize))]
    [HarmonyPostfix]
    public static void OnFinalizePostfix(ref ClanPartiesVM __instance)
    {
        var partyComponentNewLimits = new Dictionary<PartyComponent, int>();
        foreach (var partyItem in __instance.Parties)
        {
            var partyComponent = partyItem.ExpenseItem._mobileParty._partyComponent;
            int newLimit = 0;
            if (partyComponent is LordPartyComponent)
            {
                newLimit = partyComponent.WagePaymentLimit;
            }
            else if (partyComponent is GarrisonPartyComponent component)
            {
                newLimit = component.Settlement.GarrisonWagePaymentLimit;
            }

            partyComponentNewLimits[partyComponent] = newLimit;
        }

        // Send new party wage limit settings to server to sync
        // Do this as part of the finalization to avoid syncing sliders every time they change
        var message = new ClanPartiesVMFinalized(partyComponentNewLimits);
        MessageBroker.Instance.Publish(__instance, message);
    }
}
