using Common;
using Common.Messaging;
using GameInterface.Services.Clans.Extensions;
using GameInterface.Services.Heroes.Extensions;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.UI.Notifications.Messages;
using HarmonyLib;
using SandBox.CampaignBehaviors;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Services.UI.Notifications.Patches;

/// <summary>
/// Adds postfixes to events called on the server to run the same events on clients to display notifications and other processing.
/// Vanilla implementation for most of these already checks for local player's hero, clan and party
/// so usually don't need extra processing on clients for these notifications.
/// Commented events are yet to be implemented or are managed elsewhere
/// </summary>
[HarmonyPatch(typeof(DefaultNotificationsCampaignBehavior))]
internal class DefaultNotificationsCampaignBehaviorPatches
{
    [HarmonyPatch(nameof(DefaultNotificationsCampaignBehavior.OnAllianceStarted))]
    [HarmonyPostfix]
    public static void OnAllianceStartedPostfix(ref DefaultNotificationsCampaignBehavior __instance, Kingdom kingdom1, Kingdom kingdom2)
    {
        if (ModInformation.IsClient) return;

        var message = new NotifyAllianceStarted(kingdom1, kingdom2);
        MessageBroker.Instance.Publish(__instance, message);
    }

    [HarmonyPatch(nameof(DefaultNotificationsCampaignBehavior.OnAllianceEnded))]
    [HarmonyPostfix]
    public static void OnAllianceEndedPostfix(ref DefaultNotificationsCampaignBehavior __instance, Kingdom kingdom1, Kingdom kingdom2)
    {
        if (ModInformation.IsClient) return;

        var message = new NotifyAllianceEnded(kingdom1, kingdom2);
        MessageBroker.Instance.Publish(__instance, message);
    }

    [HarmonyPatch(nameof(DefaultNotificationsCampaignBehavior.OnCallToWarAgreementStarted))]
    [HarmonyPostfix]
    public static void OnCallToWarAgreementStartedPostfix(ref DefaultNotificationsCampaignBehavior __instance, Kingdom callingKingdom, Kingdom calledKingdom, Kingdom kingdomToCallToWarAgainst)
    {
        if (ModInformation.IsClient) return;

        var message = new NotifyCallWarToWarAgreementStarted(callingKingdom, calledKingdom, kingdomToCallToWarAgainst);
        MessageBroker.Instance.Publish(__instance, message);
    }

    [HarmonyPatch(nameof(DefaultNotificationsCampaignBehavior.OnCallToWarAgreementEnded))]
    [HarmonyPostfix]
    public static void OnCallToWarAgreementEndedPostfix(ref DefaultNotificationsCampaignBehavior __instance, Kingdom callingKingdom, Kingdom calledKingdom, Kingdom kingdomToCallToWarAgainst)
    {
        if (ModInformation.IsClient) return;

        var message = new NotifyCallWarToWarAgreementEnded(callingKingdom, calledKingdom, kingdomToCallToWarAgainst);
        MessageBroker.Instance.Publish(__instance, message);
    }

    [HarmonyPatch(nameof(DefaultNotificationsCampaignBehavior.OnSettlementEntered))]
    [HarmonyPostfix]
    public static void OnSettlementEnteredPostfix(ref DefaultNotificationsCampaignBehavior __instance, MobileParty mobileParty, Settlement settlement, Hero hero)
    {
        if (ModInformation.IsClient 
            || mobileParty == null || !mobileParty.IsTargetingPort
            || settlement.SiegeEvent == null || !settlement.SiegeEvent.IsBlockadeActive) return;

        var message = new NotifySettlementEntered(mobileParty, settlement, hero);
        MessageBroker.Instance.Publish(__instance, message);
    }

    [HarmonyPatch(nameof(DefaultNotificationsCampaignBehavior.OnPartyAddedToMapEvent))]
    [HarmonyPostfix]
    public static void OnPartyAddedToMapEventPostfix(ref DefaultNotificationsCampaignBehavior __instance, PartyBase involvedParty)
    {
        if (ModInformation.IsClient || !IsValidPlayerClan(involvedParty.LeaderHero.Clan)) return;

        var message = new NotifyPartyAddedToMapEvent(involvedParty);
        MessageBroker.Instance.Publish(__instance, message);
    }

    // OnCompanionRemoved (managed with RemoveCompanionActionPatch)

    // OnIssueUpdated

    // OnQuestLogAdded

    // OnQuestCompleted

    // OnQuestStarted

    [HarmonyPatch(nameof(DefaultNotificationsCampaignBehavior.OnRenownGained))]
    [HarmonyPostfix]
    public static void OnRenownGainedPostfix(ref DefaultNotificationsCampaignBehavior __instance, Hero hero, int gainedRenown, bool doNotNotifyPlayer)
    {
        if (ModInformation.IsClient || !IsValidPlayerClan(hero.Clan)) return;

        var message = new NotifyRenownGained(hero, gainedRenown, doNotNotifyPlayer);
        MessageBroker.Instance.Publish(__instance, message);
    }

    [HarmonyPatch(nameof(DefaultNotificationsCampaignBehavior.OnHideoutSpotted))]
    [HarmonyPostfix]
    public static void OnHideoutSpottedPostfix(ref DefaultNotificationsCampaignBehavior __instance, PartyBase party, PartyBase hideoutParty)
    {
        if (ModInformation.IsClient || !IsValidPlayerParty(party?.MobileParty)) return;

        var message = new NotifyHideoutSpotted(party, hideoutParty);
        MessageBroker.Instance.Publish(__instance, message);
    }

    [HarmonyPatch(nameof(DefaultNotificationsCampaignBehavior.OnHeroBecameFugitive))]
    [HarmonyPostfix]
    public static void OnHeroBecameFugitivePostfix(ref DefaultNotificationsCampaignBehavior __instance, Hero hero, bool showNotification)
    {
        if (ModInformation.IsClient || !IsValidPlayerClan(hero.Clan)) return;

        var message = new NotifyHeroBecameFugitive(hero, showNotification);
        MessageBroker.Instance.Publish(__instance, message);
    }

    [HarmonyPatch(nameof(DefaultNotificationsCampaignBehavior.OnPrisonerTaken))]
    [HarmonyPostfix]
    public static void OnPrisonerTakenPostfix(ref DefaultNotificationsCampaignBehavior __instance, PartyBase capturer, Hero prisoner)
    {
        if (ModInformation.IsClient || !IsValidPlayerClan(prisoner.Clan)) return;

        var message = new NotifyPrisonerTaken(capturer, prisoner);
        MessageBroker.Instance.Publish(__instance, message);
    }

    [HarmonyPatch(nameof(DefaultNotificationsCampaignBehavior.OnHeroPrisonerReleased))]
    [HarmonyPostfix]
    public static void OnHeroPrisonerReleasedPostfix(ref DefaultNotificationsCampaignBehavior __instance, Hero hero, PartyBase party, IFaction capturerFaction, EndCaptivityDetail detail, bool showNotification)
    {
        if (ModInformation.IsClient) return;

        var message = new NotifyHeroPrisonerReleased(hero, party, capturerFaction, detail, showNotification);
        MessageBroker.Instance.Publish(__instance, message);
    }

    [HarmonyPatch(nameof(DefaultNotificationsCampaignBehavior.OnBattleStarted))]
    [HarmonyPostfix]
    public static void OnBattleStartedPostfix(ref DefaultNotificationsCampaignBehavior __instance, PartyBase attackerParty, PartyBase defenderParty, object subject, bool showNotification)
    {
        Settlement settlement;
        if (ModInformation.IsClient || (settlement = (subject as Settlement)) == null || !IsValidPlayerClan(settlement.OwnerClan)) return;

        var message = new NotifyBattleStarted(attackerParty, defenderParty, settlement, showNotification);
        MessageBroker.Instance.Publish(__instance, message);
    }

    [HarmonyPatch(nameof(DefaultNotificationsCampaignBehavior.OnSiegeEventStarted))]
    [HarmonyPostfix]
    public static void OnSiegeEventStartedPostfix(ref DefaultNotificationsCampaignBehavior __instance, SiegeEvent siegeEvent)
    {
        if (ModInformation.IsClient || !IsValidPlayerClan(siegeEvent.BesiegedSettlement.OwnerClan)) return;

        var message = new NotifySiegeEventStarted(siegeEvent);
        MessageBroker.Instance.Publish(__instance, message);
    }

    [HarmonyPatch(nameof(DefaultNotificationsCampaignBehavior.OnClanTierIncreased))]
    [HarmonyPostfix]
    public static void OnClanTierIncreasedPostfix(ref DefaultNotificationsCampaignBehavior __instance, Clan clan, bool shouldNotify = true)
    {
        if (ModInformation.IsClient || !IsValidPlayerClan(clan)) return;

        var message = new NotifyClanTierIncreased(clan, shouldNotify);
        MessageBroker.Instance.Publish(__instance, message);
    }

    [HarmonyPatch(nameof(DefaultNotificationsCampaignBehavior.OnItemsLooted))]
    [HarmonyPostfix]
    public static void OnItemsLootedPostfix(ref DefaultNotificationsCampaignBehavior __instance, MobileParty mobileParty, ItemRoster items)
    {
        if (ModInformation.IsClient || !IsValidPlayerParty(mobileParty)) return;

        var message = new NotifyItemsLooted(mobileParty, items._data);
        MessageBroker.Instance.Publish(__instance, message);
    }

    [HarmonyPatch(nameof(DefaultNotificationsCampaignBehavior.OnRelationChanged))]
    [HarmonyPostfix]
    public static void OnRelationChangedPostfix(ref DefaultNotificationsCampaignBehavior __instance, Hero effectiveHero, Hero effectiveHeroGainedRelationWith, int relationChange, bool showNotification, ChangeRelationAction.ChangeRelationDetail detail, Hero originalHero, Hero originalGainedRelationWith)
    {
        if (ModInformation.IsClient
            || !IsValidPlayerHero(effectiveHero) && !IsValidPlayerHero(effectiveHeroGainedRelationWith)
            && !IsValidPlayerHero(originalHero) && !IsValidPlayerHero(originalGainedRelationWith)) return;

        var message = new NotifyRelationChanged(effectiveHero, effectiveHeroGainedRelationWith, relationChange, showNotification, detail, originalHero, originalGainedRelationWith);
        MessageBroker.Instance.Publish(__instance, message);
    }

    [HarmonyPatch(nameof(DefaultNotificationsCampaignBehavior.OnHeroLevelledUp))]
    [HarmonyPostfix]
    public static void OnHeroLevelledUpPostfix(ref DefaultNotificationsCampaignBehavior __instance, Hero hero, bool shouldNotify)
    {
        if (ModInformation.IsClient 
            || (!IsValidPlayerHero(hero) && !IsValidPlayerClan(hero.Clan))) return;

        var message = new NotifyHeroLevelledUp(hero, shouldNotify);
        MessageBroker.Instance.Publish(__instance, message);
    }

    [HarmonyPatch(nameof(DefaultNotificationsCampaignBehavior.OnHeroGainedSkill))]
    [HarmonyPostfix]
    public static void OnHeroGainedSkillPostfix(ref DefaultNotificationsCampaignBehavior __instance, Hero hero, SkillObject skill, int change = 1, bool shouldNotify = true)
    {
        if (ModInformation.IsClient 
            || (!IsValidPlayerHero(hero) && !IsValidPlayerClan(hero.Clan) && !IsValidPlayerParty(hero.PartyBelongedTo))) return;

        var message = new NotifyHeroGainedSkill(hero, skill, change, shouldNotify);
        MessageBroker.Instance.Publish(__instance, message);
    }

    [HarmonyPatch(nameof(DefaultNotificationsCampaignBehavior.OnTroopsDeserted))]
    [HarmonyPostfix]
    public static void OnTroopsDesertedPostfix(ref DefaultNotificationsCampaignBehavior __instance, MobileParty mobileParty, TroopRoster desertedTroops)
    {
        if (ModInformation.IsClient 
            || (!IsValidPlayerParty(mobileParty) && !IsValidPlayerHero(mobileParty.Party.Owner))) return;

        var message = new NotifyTroopsDeserted(mobileParty, desertedTroops);
        MessageBroker.Instance.Publish(__instance, message);
    }

    [HarmonyPatch(nameof(DefaultNotificationsCampaignBehavior.OnClanChangedFaction))]
    [HarmonyPostfix]
    public static void OnClanChangedFactionPostfix(ref DefaultNotificationsCampaignBehavior __instance, Clan clan, Kingdom oldKingdom, Kingdom newKingdom, ChangeKingdomAction.ChangeKingdomActionDetail detail, bool showNotification = true)
    {
        if (ModInformation.IsClient) return;

        var message = new NotifyClanChangedFaction(clan, oldKingdom, newKingdom, detail, showNotification);
        MessageBroker.Instance.Publish(__instance, message);
    }

    [HarmonyPatch(nameof(DefaultNotificationsCampaignBehavior.OnArmyCreated))]
    [HarmonyPostfix]
    public static void OnArmyCreatedPostfix(ref DefaultNotificationsCampaignBehavior __instance, Army army)
    {
        if (ModInformation.IsClient) return;

        var message = new NotifyArmyCreated(army);
        MessageBroker.Instance.Publish(__instance, message);
    }

    [HarmonyPatch(nameof(DefaultNotificationsCampaignBehavior.OnSiegeBombardmentHit))]
    [HarmonyPostfix]
    public static void OnSiegeBombardmentHitPostfix(ref DefaultNotificationsCampaignBehavior __instance, MobileParty besiegerParty, Settlement besiegedSettlement, BattleSideEnum side, SiegeEngineType weapon, SiegeBombardTargets target)
    {
        if (ModInformation.IsClient) return;

        var message = new NotifySiegeBombardmentHit(besiegerParty, besiegedSettlement, side, weapon, target);
        MessageBroker.Instance.Publish(__instance, message);
    }

    [HarmonyPatch(nameof(DefaultNotificationsCampaignBehavior.OnSiegeBombardmentWallHit))]
    [HarmonyPostfix]
    public static void OnSiegeBombardmentWallHitPostfix(ref DefaultNotificationsCampaignBehavior __instance, MobileParty besiegerParty, Settlement besiegedSettlement, BattleSideEnum side, SiegeEngineType weapon, bool isWallCracked)
    {
        if (ModInformation.IsClient) return;

        var message = new NotifySiegeBombardmentWallHit(besiegerParty, besiegedSettlement, side, weapon, isWallCracked);
        MessageBroker.Instance.Publish(__instance, message);
    }

    [HarmonyPatch(nameof(DefaultNotificationsCampaignBehavior.OnSiegeEngineDestroyed))]
    [HarmonyPostfix]
    public static void OnSiegeEngineDestroyedPostfix(ref DefaultNotificationsCampaignBehavior __instance, MobileParty besiegerParty, Settlement besiegedSettlement, BattleSideEnum side, SiegeEngineType destroyedEngine)
    {
        if (ModInformation.IsClient) return;

        var message = new NotifySiegeEngineDestroyed(besiegerParty, besiegedSettlement, side, destroyedEngine);
        MessageBroker.Instance.Publish(__instance, message);
    }

    // OnHeroOrPartyTradedGold (managed with GiveGoldActionPatch)

    [HarmonyPatch(nameof(DefaultNotificationsCampaignBehavior.OnPartyJoinedArmy))]
    [HarmonyPostfix]
    public static void OnPartyJoinedArmyPostfix(ref DefaultNotificationsCampaignBehavior __instance, MobileParty party)
    {
        if (ModInformation.IsClient) return;

        var message = new NotifyPartyJoinedArmy(party);
        MessageBroker.Instance.Publish(__instance, message);
    }

    [HarmonyPatch(nameof(DefaultNotificationsCampaignBehavior.OnPartyAttachedAnotherParty))]
    [HarmonyPostfix]
    public static void OnPartyAttachedAnotherPartyPostfix(ref DefaultNotificationsCampaignBehavior __instance, MobileParty party)
    {
        if (ModInformation.IsClient) return;

        var message = new NotifyPartyAttachedAnotherParty(party);
        MessageBroker.Instance.Publish(__instance, message);
    }

    [HarmonyPatch(nameof(DefaultNotificationsCampaignBehavior.OnPartyRemovedFromArmy))]
    [HarmonyPostfix]
    public static void OnPartyRemovedFromArmyPostfix(ref DefaultNotificationsCampaignBehavior __instance, MobileParty party)
    {
        if (ModInformation.IsClient) return;

        var message = new NotifyPartyRemovedFromArmy(party);
        MessageBroker.Instance.Publish(__instance, message);
    }

    [HarmonyPatch(nameof(DefaultNotificationsCampaignBehavior.OnArmyDispersed))]
    [HarmonyPostfix]
    public static void OnArmyDispersedPostfix(ref DefaultNotificationsCampaignBehavior __instance, Army army, Army.ArmyDispersionReason reason, bool isPlayersArmy)
    {
        if (ModInformation.IsClient) return;

        var message = new ArmyDispersed(); // Needs to check food notifications
        MessageBroker.Instance.Publish(__instance, message);
    }

    [HarmonyPatch(nameof(DefaultNotificationsCampaignBehavior.OnHeroesMarried))]
    [HarmonyPostfix]
    public static void OnHeroesMarriedPostfix(ref DefaultNotificationsCampaignBehavior __instance, Hero firstHero, Hero secondHero, bool showNotification)
    {
        if (ModInformation.IsClient 
            || (!IsValidPlayerClan(firstHero.Clan) && !IsValidPlayerClan(secondHero.Clan))) return;

        var message = new NotifyHeroesMarried(firstHero, secondHero, showNotification);
        MessageBroker.Instance.Publish(__instance, message);
    }

    // OnSettlementOwnerChanged (managed with SettlementOwnershipHandler)

    [HarmonyPatch(nameof(DefaultNotificationsCampaignBehavior.OnChildConceived))]
    [HarmonyPostfix]
    public static void OnChildConceivedPostfix(ref DefaultNotificationsCampaignBehavior __instance, Hero mother)
    {
        if (ModInformation.IsClient) return;

        var message = new NotifyChildConceived(mother);
        MessageBroker.Instance.Publish(__instance, message);
    }

    [HarmonyPatch(nameof(DefaultNotificationsCampaignBehavior.OnGivenBirth))]
    [HarmonyPostfix]
    public static void OnGivenBirthPostfix(ref DefaultNotificationsCampaignBehavior __instance, Hero mother, List<Hero> aliveOffsprings, int stillbornCount)
    {
        if (ModInformation.IsClient) return;

        var message = new NotifyGivenBirth(mother, aliveOffsprings, stillbornCount);
        MessageBroker.Instance.Publish(__instance, message);
    }

    [HarmonyPatch(nameof(DefaultNotificationsCampaignBehavior.OnHeroKilled))]
    [HarmonyPostfix]
    public static void OnHeroKilledPostfix(ref DefaultNotificationsCampaignBehavior __instance, Hero victimHero, Hero killer, KillCharacterAction.KillCharacterActionDetail detail, bool showNotification)
    {
        if (ModInformation.IsClient || !IsValidPlayerClan(victimHero.Clan)) return;

        var message = new NotifyHeroKilled(victimHero, killer, detail, showNotification);
        MessageBroker.Instance.Publish(__instance, message);
    }

    [HarmonyPatch(nameof(DefaultNotificationsCampaignBehavior.OnHeroSharedFoodWithAnotherHero))]
    [HarmonyPostfix]
    public static void OnHeroSharedFoodWithAnotherHeroPostfix(ref DefaultNotificationsCampaignBehavior __instance, Hero supporterHero, Hero supportedHero, float influence)
    {
        if (ModInformation.IsClient 
            || !IsValidPlayerHero(supporterHero) && !IsValidPlayerHero(supportedHero)) return;

        var message = new HeroSharedFoodWithAnotherHero(supporterHero, supportedHero, influence);
        MessageBroker.Instance.Publish(__instance, message);
    }

    [HarmonyPatch(nameof(DefaultNotificationsCampaignBehavior.OnClanDestroyed))]
    [HarmonyPostfix]
    public static void OnClanDestroyedPostfix(ref DefaultNotificationsCampaignBehavior __instance, Clan destroyedClan)
    {
        if (ModInformation.IsClient) return;

        var message = new NotifyClanDestroyed(destroyedClan);
        MessageBroker.Instance.Publish(__instance, message);
    }

    [HarmonyPatch(nameof(DefaultNotificationsCampaignBehavior.OnHeroOrPartyGaveItem))]
    [HarmonyPostfix]
    public static void OnHeroOrPartyGaveItemPostfix(ref DefaultNotificationsCampaignBehavior __instance, ValueTuple<Hero, PartyBase> giver, ValueTuple<Hero, PartyBase> receiver, ItemRosterElement itemRosterElement, bool showNotification)
    {
        if (ModInformation.IsClient) return;

        var message = new NotifyHeroOrPartyGaveItem(giver, receiver, itemRosterElement, showNotification);
        MessageBroker.Instance.Publish(__instance, message);
    }

    [HarmonyPatch(nameof(DefaultNotificationsCampaignBehavior.OnRebellionFinished))]
    [HarmonyPostfix]
    public static void OnRebellionFinishedPostfix(ref DefaultNotificationsCampaignBehavior __instance, Settlement settlement, Clan oldOwnerClan)
    {
        if (ModInformation.IsClient) return;

        var message = new NotifyRebellionFinished(settlement, oldOwnerClan);
        MessageBroker.Instance.Publish(__instance, message);
    }

    [HarmonyPatch(nameof(DefaultNotificationsCampaignBehavior.OnTournamentFinished))]
    [HarmonyPostfix]
    public static void OnTournamentFinishedPostfix(ref DefaultNotificationsCampaignBehavior __instance, CharacterObject winner, MBReadOnlyList<CharacterObject> participants, Town town, ItemObject prize)
    {
        if (ModInformation.IsClient 
            || !winner.IsHero || !IsValidPlayerClan(winner.HeroObject.Clan) || !IsValidPlayerParty(winner.HeroObject.PartyBelongedTo)) return;

        var message = new NotifyTournamentFinished(winner, participants, town, prize);
        MessageBroker.Instance.Publish(__instance, message);
    }

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

    // Helper functions to avoid logging errors about heroes, parties and clans being null
    private static bool IsValidPlayerHero(Hero hero)
    {
        return hero != null && hero.IsPlayerHero();
    }

    private static bool IsValidPlayerParty(MobileParty mobileParty)
    {
        return mobileParty != null && mobileParty.IsPlayerParty();
    }

    private static bool IsValidPlayerClan(Clan clan)
    {
        return clan != null && clan.IsPlayerClan();
    }
}
