using Common;
using Common.Messaging;
using GameInterface.Services.UI.Notifications.Messages;
using HarmonyLib;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace GameInterface.Services.UI.Notifications.Patches;

/// <summary>
/// The campaign events driving the settlement nameplate popups (caravan trades, recruitment,
/// prisoner sales, garrison donations) are only raised by server-side AI logic, so the popups
/// never show on clients. Forward them so clients can re-raise the events for their nameplate UI.
/// </summary>
[HarmonyPatch(typeof(CampaignEventDispatcher))]
internal class SettlementNotificationPatches
{
    [HarmonyPatch(nameof(CampaignEventDispatcher.OnCaravanTransactionCompleted))]
    [HarmonyPostfix]
    private static void OnCaravanTransactionCompletedPostfix(MobileParty caravanParty, Town town, List<(EquipmentElement, int)> itemRosterElements)
    {
        if (ModInformation.IsClient) return;
        if (caravanParty == null || town == null) return;

        MessageBroker.Instance.Publish(null, new NotifyCaravanTransaction(caravanParty, town, itemRosterElements));
    }

    [HarmonyPatch(nameof(CampaignEventDispatcher.OnTroopRecruited))]
    [HarmonyPostfix]
    private static void OnTroopRecruitedPostfix(Hero recruiterHero, Settlement recruitmentSettlement, Hero recruitmentSource, CharacterObject troop, int amount)
    {
        if (ModInformation.IsClient) return;
        // The nameplate popup requires both; the prisoner-recruit path passes a null settlement
        if (recruiterHero == null || recruitmentSettlement == null || troop == null) return;

        MessageBroker.Instance.Publish(null, new NotifyTroopRecruited(recruiterHero, recruitmentSettlement, recruitmentSource, troop, amount));
    }

    [HarmonyPatch(nameof(CampaignEventDispatcher.OnPrisonerSold))]
    [HarmonyPostfix]
    private static void OnPrisonerSoldPostfix(PartyBase sellerParty, PartyBase buyerParty, TroopRoster prisoners)
    {
        if (ModInformation.IsClient) return;
        if (sellerParty == null || buyerParty == null || prisoners == null) return;

        MessageBroker.Instance.Publish(null, new NotifyPrisonerSold(sellerParty, buyerParty, prisoners));
    }

    [HarmonyPatch(nameof(CampaignEventDispatcher.OnTroopGivenToSettlement))]
    [HarmonyPostfix]
    private static void OnTroopGivenToSettlementPostfix(Hero giverHero, Settlement recipientSettlement, TroopRoster roster)
    {
        if (ModInformation.IsClient) return;
        if (giverHero == null || recipientSettlement == null || roster == null) return;

        MessageBroker.Instance.Publish(null, new NotifyTroopGivenToSettlement(giverHero, recipientSettlement, roster));
    }
}
