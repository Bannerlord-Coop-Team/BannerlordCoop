using Common;
using Common.Messaging;
using GameInterface.Services.Clans.Extensions;
using GameInterface.Services.Heroes.Extensions;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.UI.Notifications.Messages;
using HarmonyLib;
using SandBox.CampaignBehaviors;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;

namespace GameInterface.Services.UI.Notifications.Patches;

/// <summary>
/// Adds postfixes to events called on the server to send notifications to clients.
/// Vanilla implementation for most of these already check for local player's hero, clan and party
/// Commented notifications are yet to be implemented or are managed elsewhere
/// </summary>
[HarmonyPatch(typeof(DefaultNotificationsCampaignBehavior))]
internal class DefaultNotificationsCampaignBehaviorPatches
{
    // OnAllianceStarted

    // OnAllianceEnded

    // OnCallToWarAgreementStarted

    // OnCallToWarAgreementEnded

    // OnSettlementEntered

    // OnPartyAddedToMapEvent

    // OnCompanionRemoved (managed with RemoveCompanionActionPatch)

    // OnIssueUpdated

    // OnQuestLogAdded

    // OnQuestCompleted

    // OnQuestStarted

    // OnRenownGained

    // OnHideoutSpotted

    // OnHeroBecameFugitive

    // OnPrisonerTaken

    // OnHeroPrisonerReleased

    // OnBattleStarted

    // OnSiegeEventStarted

    [HarmonyPatch(nameof(DefaultNotificationsCampaignBehavior.OnClanTierIncreased))]
    [HarmonyPostfix]
    public static void OnClanTierIncreasedPostfix(ref DefaultNotificationsCampaignBehavior __instance, Clan clan, bool shouldNotify = true)
    {
        if (ModInformation.IsClient || clan == null || !clan.IsPlayerClan()) return;

        var message = new NotifyClanTierIncreased(clan, shouldNotify);
        MessageBroker.Instance.Publish(__instance, message);
    }

    // OnItemsLooted

    [HarmonyPatch(nameof(DefaultNotificationsCampaignBehavior.OnRelationChanged))]
    [HarmonyPostfix]
    public static void OnRelationChangedPostfix(ref DefaultNotificationsCampaignBehavior __instance, Hero effectiveHero, Hero effectiveHeroGainedRelationWith, int relationChange, bool showNotification, ChangeRelationAction.ChangeRelationDetail detail, Hero originalHero, Hero originalGainedRelationWith)
    {
        if (ModInformation.IsClient) return;

        // Don't notify clients of relation changes that don't involve players
        if (!IsValidPlayerHero(effectiveHero) && !IsValidPlayerHero(effectiveHeroGainedRelationWith)
            && !IsValidPlayerHero(originalHero) && !IsValidPlayerHero(originalGainedRelationWith)) return;

        var message = new NotifyRelationChanged(effectiveHero, effectiveHeroGainedRelationWith, relationChange, showNotification, detail, originalHero, originalGainedRelationWith);
        MessageBroker.Instance.Publish(__instance, message);
    }

    // OnHeroLevelledUp

    // OnHeroGainedSkill

    [HarmonyPatch(nameof(DefaultNotificationsCampaignBehavior.OnTroopsDeserted))]
    [HarmonyPostfix]
    public static void OnTroopsDesertedPostfix(ref DefaultNotificationsCampaignBehavior __instance, MobileParty mobileParty, TroopRoster desertedTroops)
    {
        if (ModInformation.IsClient || mobileParty == null || !mobileParty.IsPlayerParty()) return;

        var message = new NotifyTroopsDeserted(mobileParty, desertedTroops);
        MessageBroker.Instance.Publish(__instance, message);
    }

    // OnClanChangedFaction

    // OnRegularClanChangedKingdom

    // OnMercenaryClanChangedKingdom

    // OnArmyCreated

    // OnSiegeBombardmentHit

    // OnSiegeBombardmentWallHit

    // OnSiegeEngineDestroyed

    // OnHeroOrPartyTradedGold (managed with GiveGoldActionPatch)

    // OnPartyJoinedArmy

    // OnPartyAttachedAnotherParty

    // OnPartyRemovedFromArmy

    // OnArmyDispersed

    // OnHeroesMarried

    // OnSettlementOwnerChanged (managed with SettlementOwnershipHandler)

    // OnChildConceived

    // OnGivenBirth

    // OnHeroKilled

    // OnHeroSharedFoodWithAnotherHero

    [HarmonyPatch(nameof(DefaultNotificationsCampaignBehavior.OnClanDestroyed))]
    [HarmonyPostfix]
    public static void OnClanDestroyedPostfix(ref DefaultNotificationsCampaignBehavior __instance, Clan destroyedClan)
    {
        if (ModInformation.IsClient) return;

        var message = new NotifyClanDestroyed(destroyedClan.Name);
        MessageBroker.Instance.Publish(__instance, message);
    }

    // OnHeroOrPartyGaveItem

    // OnRebellionFinished

    // OnTournamentFinished

    [HarmonyPatch(nameof(DefaultNotificationsCampaignBehavior.OnBuildingLevelChanged))]
    [HarmonyPostfix]
    public static void OnBuildingLevelChangedPostfix(ref DefaultNotificationsCampaignBehavior __instance, Town town, Building building, int levelChange)
    {
        if (ModInformation.IsClient || town.OwnerClan == null || !town.OwnerClan.IsPlayerClan()) return;

        var message = new NotifyBuildingLevelChanged(town, building, levelChange);
        MessageBroker.Instance.Publish(__instance, message);
    }

    [HarmonyPatch(nameof(DefaultNotificationsCampaignBehavior.OnHeroTeleportationRequested))]
    [HarmonyPostfix]
    public static void OnHeroTeleportationRequestedPostfix(ref DefaultNotificationsCampaignBehavior __instance, Hero hero, Settlement targetSettlement, MobileParty targetParty, TeleportHeroAction.TeleportationDetail detail)
    {
        if (ModInformation.IsClient || hero.Clan == null || !hero.Clan.IsPlayerClan()) return;

        var message = new NotifyHeroTeleportation(hero, targetSettlement, targetParty, detail);
        MessageBroker.Instance.Publish(__instance, message);
    }

    // Helper function to avoid logging errors about heroes and parties being null
    private static bool IsValidPlayerHero(Hero hero)
    {
        return hero != null && hero.IsPlayerHero();
    }
}
