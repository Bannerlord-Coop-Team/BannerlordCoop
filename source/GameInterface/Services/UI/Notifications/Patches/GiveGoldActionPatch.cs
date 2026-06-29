using Common;
using Common.Messaging;
using GameInterface.Services.Heroes.Extensions;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.UI.Notifications.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.UI.Notifications.Patches;

/// <summary>
/// Adds a postfix to send a notification when GiveGoldAction.ApplyInternal gives or takes gold from a player or players.
/// Also overrides other GiveGoldAction methods that in TaleWorlds' implementation use Hero.MainHero to block notifications.
/// Patching all methods instead of forcing the notification regardless of showQuickInformation in ApplyInternal
/// gives the option to disable notifications for player transactions if needed.
/// </summary>
[HarmonyPatch(typeof(GiveGoldAction))]
internal class GiveGoldActionPatches
{
    [HarmonyPatch(nameof(GiveGoldAction.ApplyInternal))]
    [HarmonyPostfix]
    public static void ApplyInternalPostfix(Hero giverHero, PartyBase giverParty, Hero recipientHero, PartyBase recipientParty, int goldAmount, bool showQuickInformation, string transactionStringId = "")
    {
        if (ModInformation.IsClient) return;

        // Don't notify players of gold changes that don't involve any players
        if (!IsValidPlayerHero(giverHero)
            && !IsValidPlayerParty(giverParty)
            && !IsValidPlayerHero(recipientHero)
            && !IsValidPlayerParty(recipientParty)) return;

        var message = new NotifyGoldChanged(giverHero, giverParty, recipientHero, recipientParty, goldAmount, showQuickInformation);
        MessageBroker.Instance.Publish(null, message);
    }

    [HarmonyPatch(nameof(GiveGoldAction.ApplyBetweenCharacters))]
    [HarmonyPrefix]
    public static bool ApplyBetweenCharactersPrefix(Hero giverHero, Hero recipientHero, int amount, bool disableNotification = false)
    {
        GiveGoldAction.ApplyInternal(giverHero, null, recipientHero, null, amount, !disableNotification, "");
        return false;
    }

    [HarmonyPatch(nameof(GiveGoldAction.ApplyForCharacterToSettlement))]
    [HarmonyPrefix]
    public static bool ApplyForCharacterToSettlementPrefix(Hero giverHero, Settlement settlement, int amount, bool disableNotification = false)
    {
        GiveGoldAction.ApplyInternal(giverHero, null, null, settlement.Party, amount, !disableNotification, "");
        return false;
    }

    [HarmonyPatch(nameof(GiveGoldAction.ApplyForSettlementToParty))]
    [HarmonyPrefix]
    public static bool ApplyForSettlementToPartyPrefix(Settlement giverSettlement, PartyBase recipientParty, int amount, bool disableNotification = false)
    {
        GiveGoldAction.ApplyInternal(null, giverSettlement.Party, null, recipientParty, amount, !disableNotification, "");
        return false;
    }

    [HarmonyPatch(nameof(GiveGoldAction.ApplyForPartyToSettlement))]
    [HarmonyPrefix]
    public static bool ApplyForPartyToSettlementPrefix(PartyBase giverParty, Settlement settlement, int amount, bool disableNotification = false)
    {
        GiveGoldAction.ApplyInternal(null, giverParty, null, settlement.Party, amount, !disableNotification, "");
        return false;
    }

    [HarmonyPatch(nameof(GiveGoldAction.ApplyForPartyToCharacter))]
    [HarmonyPrefix]
    public static bool ApplyForPartyToCharacterPrefix(PartyBase giverParty, Hero recipientHero, int amount, bool disableNotification = false)
    {
        GiveGoldAction.ApplyInternal(null, giverParty, recipientHero, null, amount, !disableNotification, "");
        return false;
    }

    [HarmonyPatch(nameof(GiveGoldAction.ApplyForCharacterToParty))]
    [HarmonyPrefix]
    public static bool ApplyForCharacterToPartyPrefix(Hero giverHero, PartyBase receipentParty, int amount, bool disableNotification = false)
    {
        GiveGoldAction.ApplyInternal(giverHero, null, null, receipentParty, amount, !disableNotification, ""); // Typo is in TaleWorlds code
        return false;
    }

    [HarmonyPatch(nameof(GiveGoldAction.ApplyForPartyToParty))]
    [HarmonyPrefix]
    public static bool ApplyForPartyToPartyPrefix(PartyBase giverParty, PartyBase receipentParty, int amount, bool disableNotification = false)
    {
        GiveGoldAction.ApplyInternal(null, giverParty, null, receipentParty, amount, !disableNotification, ""); // Typo is in TaleWorlds code
        return false;
    }

    // Helper functions to avoid logging errors about heroes and parties being null
    private static bool IsValidPlayerHero(Hero hero)
    {
        return hero != null && hero.IsPlayerHero();
    }

    private static bool IsValidPlayerParty(PartyBase party)
    {
        return party != null && party.MobileParty != null && party.MobileParty.IsPlayerParty();
    }
}