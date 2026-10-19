using Common;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using Quest = TaleWorlds.CampaignSystem.Issues.MerchantArmyOfPoachersIssueBehavior.MerchantArmyOfPoachersIssueQuest;

namespace GameInterface.Services.Issues.Quests.ArmyOfPoachers.Patches;

[HarmonyPatch]
internal class PoachersQuestEventPatches
{
    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnMapEventStarted))]
    [HarmonyPostfix]
    private static void MapEventStartedPostfix(MapEvent mapEvent, PartyBase attackerParty, PartyBase defenderParty)
    {
        RunAsOwners(quest =>
        {
            quest.MapEventStarted(mapEvent, attackerParty, defenderParty);
            if (quest._poachersParty?.MapEvent == mapEvent && mapEvent.InvolvedParties.Contains(PartyBase.MainParty))
                quest._isReadyToBeFinalized = true;
        });
    }

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnMapEventEnded))]
    [HarmonyPostfix]
    private static void MapEventEndedPostfix(MapEvent mapEvent) => RunAsOwners(quest => quest.MapEventCheck(mapEvent));

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnWarDeclared))]
    [HarmonyPostfix]
    private static void WarDeclaredPostfix(IFaction faction1, IFaction faction2, DeclareWarAction.DeclareWarDetail declareWarDetail)
    {
        RunAsOwners(quest => quest.OnWarDeclared(faction1, faction2, declareWarDetail));
    }

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnClanChangedKingdom))]
    [HarmonyPostfix]
    private static void ClanChangedPostfix(Clan clan, Kingdom oldKingdom, Kingdom newKingdom,
        ChangeKingdomAction.ChangeKingdomActionDetail actionDetail, bool showNotification)
    {
        RunAsOwners(quest => quest.OnClanChangedKingdom(clan, oldKingdom, newKingdom, actionDetail, showNotification));
    }

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.CanHeroBecomePrisoner))]
    [HarmonyPostfix]
    private static void CanBecomePrisonerPostfix(Hero hero, ref bool result)
    {
        var canBecomePrisoner = result;
        RunAsOwners(quest => quest.OnCanHeroBecomePrisonerInfoIsRequested(hero, ref canBecomePrisoner));
        result = canBecomePrisoner;
    }

    [HarmonyPatch(typeof(Quest), nameof(Quest.MapEventStarted))]
    [HarmonyPrefix]
    private static bool MapEventStartedPrefix() => ModInformation.IsServer;

    [HarmonyPatch(typeof(Quest), nameof(Quest.MapEventCheck))]
    [HarmonyPrefix]
    private static bool MapEventCheckPrefix() => ModInformation.IsServer;

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnWarDeclared))]
    [HarmonyPrefix]
    private static bool WarDeclaredPrefix() => ModInformation.IsServer;

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnClanChangedKingdom))]
    [HarmonyPrefix]
    private static bool ClanChangedPrefix() => ModInformation.IsServer;

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnTimedOut))]
    [HarmonyPrefix]
    private static bool TimedOutPrefix() => ModInformation.IsServer;

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnFinalize))]
    [HarmonyPrefix]
    private static bool FinalizePrefix(Quest __instance)
    {
        if (ModInformation.IsServer)
        {
            if (__instance._poachersParty?.IsActive == true)
                DestroyPartyAction.Apply(null, __instance._poachersParty);

            // Endings run as the owner, where the captivity handler would take them for the server's own main hero
            if (TryGetOwner(__instance, out var hero) && hero.IsPrisoner)
                GameThread.EnqueueSafe(() =>
                {
                    if (hero.IsPrisoner) EndCaptivityAction.ApplyByPeace(hero);
                }, context: nameof(PoachersQuestEventPatches));
        }
        else if (PlayerEncounter.Current != null &&
            Campaign.Current.CurrentMenuContext?.GameMenu.StringId == "army_of_poachers_village")
        {
            PlayerEncounter.Finish();
        }

        return false;
    }

    [HarmonyPatch(typeof(MapEvent), nameof(MapEvent.FinalizeEventAux))]
    [HarmonyPrefix]
    private static void BattleFinalizingPrefix(MapEvent __instance, bool __runOriginal, out Dictionary<Quest, bool?> __state)
    {
        __state = null;
        if (ModInformation.IsClient || !__runOriginal || __instance.IsFinalized) return;

        var outcomes = new Dictionary<Quest, bool?>();
        RunAsOwners(quest =>
        {
            var poachers = quest._poachersParty?.Party;
            if (poachers == null || !__instance.InvolvedParties.Contains(poachers)) return;

            if (__instance.InvolvedParties.Contains(PartyBase.MainParty))
                outcomes[quest] = __instance.HasWinner && PartyBase.MainParty.Side == __instance.WinningSide;
            else if (quest._isReadyToBeFinalized)
                outcomes[quest] = false;
            else if (__instance.HasWinner && poachers.Side != __instance.WinningSide)
                outcomes[quest] = null;
        });
        __state = outcomes;
    }

    [HarmonyPatch(typeof(MapEvent), nameof(MapEvent.FinalizeEventAux))]
    [HarmonyPostfix]
    private static void BattleFinalizedPostfix(MapEvent __instance, bool __runOriginal, Dictionary<Quest, bool?> __state)
    {
        if (!__runOriginal || !__instance.IsFinalized || __state == null || __state.Count == 0) return;

        // An owner the poachers hold is released before failing removes them, outside the owner's scope
        foreach (var outcome in __state)
        {
            var quest = outcome.Key;
            if (outcome.Value == false && TryGetOwner(quest, out var owner) && owner.IsPrisoner &&
                owner.PartyBelongedToAsPrisoner == quest._poachersParty?.Party)
                EndCaptivityAction.ApplyByPeace(owner);
        }

        RunAsOwners(quest =>
        {
            if (!__state.TryGetValue(quest, out var won)) return;
            if (!won.HasValue)
                quest.CompleteQuestWithCancel();
            else if (won.Value)
                quest.QuestSuccessWithPlayerDefeatedPoachers();
            else
                quest.QuestFailWithPlayerDefeatedAgainstPoachers();
        });
    }

    // An issue that ends under its running quest, as a dismissed one does, would leave the poachers in the village
    [HarmonyPatch(typeof(IssueBase), nameof(IssueBase.IssueFinalized))]
    [HarmonyPrefix]
    private static void IssueFinalizedPrefix(IssueBase __instance)
    {
        if (ModInformation.IsServer && __instance.IssueQuest is Quest quest && quest.IsOngoing) quest.OnFinalize();
    }

    private static bool TryGetOwner(Quest quest, out Hero owner)
    {
        owner = null;
        var issue = quest.QuestGiver.Issue;
        return issue?.IssueQuest == quest && ContainerProvider.TryResolve<IIssueOwnerResolver>(out var owners) &&
            owners.TryResolveOwnerHero(issue, out owner);
    }

    private static void RunAsOwners(Action<Quest> listener)
    {
        if (ContainerProvider.TryResolve<IOutcomeDispatcher>(out var dispatcher))
            dispatcher.RunAsOwners(listener);
    }
}
