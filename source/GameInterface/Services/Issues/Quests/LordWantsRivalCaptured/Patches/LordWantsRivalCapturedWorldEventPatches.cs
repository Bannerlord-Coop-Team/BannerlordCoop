using Common;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using Quest = TaleWorlds.CampaignSystem.Issues.LordWantsRivalCapturedIssueBehavior.LordWantsRivalCapturedIssueQuest;

namespace GameInterface.Services.Issues.Quests.LordWantsRivalCaptured.Patches;

/// <summary>
/// The server's copy of the quest is never started, and kills and captures do not run on clients, so the
/// server runs the quest's world event listeners as each owning player.
/// </summary>
[HarmonyPatch(typeof(CampaignEventDispatcher))]
internal class LordWantsRivalCapturedWorldEventPatches
{
    [HarmonyPatch(nameof(CampaignEventDispatcher.OnHeroKilled))]
    [HarmonyPostfix]
    private static void HeroKilledPostfix(Hero victim, Hero killer, KillCharacterAction.KillCharacterActionDetail detail, bool showNotification)
    {
        RunAsOwners(quest => quest.OnHeroKilled(victim, killer, detail, showNotification));
    }

    [HarmonyPatch(nameof(CampaignEventDispatcher.OnHeroPrisonerTaken))]
    [HarmonyPostfix]
    private static void HeroPrisonerTakenPostfix(PartyBase capturer, Hero prisoner)
    {
        RunAsOwners(quest => quest.OnPrisonerTaken(capturer, prisoner));
    }

    [HarmonyPatch(nameof(CampaignEventDispatcher.OnWarDeclared))]
    [HarmonyPostfix]
    private static void WarDeclaredPostfix(IFaction faction1, IFaction faction2, DeclareWarAction.DeclareWarDetail declareWarDetail)
    {
        RunAsOwners(quest => quest.OnWarDeclared(faction1, faction2, declareWarDetail));
    }

    [HarmonyPatch(nameof(CampaignEventDispatcher.OnClanChangedKingdom))]
    [HarmonyPostfix]
    private static void ClanChangedKingdomPostfix(Clan clan, Kingdom oldKingdom, Kingdom newKingdom, ChangeKingdomAction.ChangeKingdomActionDetail actionDetail, bool showNotification)
    {
        RunAsOwners(quest => quest.OnClanChangedKingdom(clan, oldKingdom, newKingdom, actionDetail, showNotification));
    }

    [HarmonyPatch(nameof(CampaignEventDispatcher.OnMapEventStarted))]
    [HarmonyPostfix]
    private static void MapEventStartedPostfix(MapEvent mapEvent, PartyBase attackerParty, PartyBase defenderParty)
    {
        RunAsOwners(quest => quest.OnMapEventStarted(mapEvent, attackerParty, defenderParty));
    }

    private static void RunAsOwners(Action<Quest> listener)
    {
        // RunAsOwners does nothing on clients
        if (!ContainerProvider.TryResolve<IOutcomeDispatcher>(out var dispatcher))
        {
            return;
        }

        dispatcher.RunAsOwners(listener);
    }
}

// The owner's client leaves the listeners that end the quest or change the world to the server
[HarmonyPatch(typeof(Quest))]
internal class LordWantsRivalCapturedClientListenerPatches
{
    [HarmonyPatch(nameof(Quest.OnHeroKilled))]
    [HarmonyPrefix]
    private static bool OnHeroKilledPrefix() => ModInformation.IsServer;

    [HarmonyPatch(nameof(Quest.OnPrisonerTaken))]
    [HarmonyPrefix]
    private static bool OnPrisonerTakenPrefix() => ModInformation.IsServer;

    [HarmonyPatch(nameof(Quest.OnWarDeclared))]
    [HarmonyPrefix]
    private static bool OnWarDeclaredPrefix() => ModInformation.IsServer;

    [HarmonyPatch(nameof(Quest.OnClanChangedKingdom))]
    [HarmonyPrefix]
    private static bool OnClanChangedKingdomPrefix() => ModInformation.IsServer;

    [HarmonyPatch(nameof(Quest.OnMapEventStarted))]
    [HarmonyPrefix]
    private static bool OnMapEventStartedPrefix() => ModInformation.IsServer;

    [HarmonyPatch(nameof(Quest.OnPlayerBattleEventEnded))]
    [HarmonyPrefix]
    private static bool OnPlayerBattleEventEndedPrefix() => ModInformation.IsServer;
}
