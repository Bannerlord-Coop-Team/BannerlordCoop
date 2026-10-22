using Common;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using Quest = TaleWorlds.CampaignSystem.Issues.LordWantsRivalCapturedIssueBehavior.LordWantsRivalCapturedIssueQuest;

namespace GameInterface.Services.Issues.Quests.LordWantsRivalCaptured.Patches;

/// <summary>
/// The server runs the world event listeners the framework's QuestEventRelay does not cover as each owning player:
/// prisoner releases and the quest's own prisoner listener (named OnPrisonerTaken, not OnHeroPrisonerTaken).
/// </summary>
[HarmonyPatch(typeof(CampaignEventDispatcher))]
internal class LordWantsRivalCapturedWorldEventPatches
{
    [HarmonyPatch(nameof(CampaignEventDispatcher.OnHeroPrisonerTaken))]
    [HarmonyPostfix]
    private static void HeroPrisonerTakenPostfix(PartyBase capturer, Hero prisoner)
    {
        RunAsOwners(quest => quest.OnPrisonerTaken(capturer, prisoner));
    }

    [HarmonyPatch(nameof(CampaignEventDispatcher.OnHeroPrisonerReleased))]
    [HarmonyPostfix]
    private static void HeroPrisonerReleasedPostfix(
        Hero prisoner,
        PartyBase party,
        IFaction capturerFaction,
        EndCaptivityDetail detail,
        bool showNotification)
    {
        RunAsOwners(quest => quest.OnHeroPrisonerReleased(prisoner, party, capturerFaction, detail, showNotification));
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

[HarmonyPatch(typeof(Quest))]
internal static class LordWantsRivalCapturedCaptureLifecyclePatches
{
    [HarmonyPatch(nameof(Quest.OnHeroPrisonerReleased))]
    [HarmonyPrefix]
    private static void OnHeroPrisonerReleasedPrefix(Hero prisoner)
    {
        if (ModInformation.IsServer && ContainerProvider.TryResolve<LordWantsRivalCapturedFinalizationProofStrategy>(out var strategy))
        {
            strategy.ClearTargetPendingCaptures(prisoner);
        }
    }

    [HarmonyPatch(nameof(Quest.OnPrisonerTaken))]
    [HarmonyPrefix]
    private static void OnPrisonerTakenPrefix(Hero prisoner)
    {
        if (ModInformation.IsServer && ContainerProvider.TryResolve<LordWantsRivalCapturedFinalizationProofStrategy>(out var strategy))
        {
            strategy.ClearTargetPendingCaptures(prisoner);
        }
    }
}

[HarmonyPatch(typeof(MakeHeroFugitiveAction), nameof(MakeHeroFugitiveAction.Apply))]
internal static class LordWantsRivalCapturedFugitivePatch
{
    [HarmonyPrefix]
    private static void Prefix(Hero fugitive)
    {
        if (ModInformation.IsServer && ContainerProvider.TryResolve<LordWantsRivalCapturedFinalizationProofStrategy>(out var strategy))
        {
            strategy.ClearTargetPendingCaptures(fugitive);
        }
    }
}

[HarmonyPatch]
internal static class LordWantsRivalCapturedFinalizationCleanupPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(QuestBase), nameof(QuestBase.CompleteQuestWithSuccess));
        yield return AccessTools.Method(typeof(QuestBase), nameof(QuestBase.CompleteQuestWithFail));
        yield return AccessTools.Method(typeof(QuestBase), nameof(QuestBase.CompleteQuestWithCancel));
        yield return AccessTools.Method(typeof(QuestBase), nameof(QuestBase.CompleteQuestWithBetrayal));
        yield return AccessTools.Method(typeof(QuestBase), nameof(QuestBase.CompleteQuestWithTimeOut));
    }

    [HarmonyPrefix]
    private static void Prefix(QuestBase __instance)
    {
        if (__instance is Quest quest && ContainerProvider.TryResolve<LordWantsRivalCapturedFinalizationProofStrategy>(out var strategy))
        {
            strategy.ClearPendingCapture(quest);
        }
    }
}

[HarmonyPatch(typeof(PlayerManager), nameof(PlayerManager.ReplacePlayer))]
internal static class LordWantsRivalCapturedPlayerReplacementPatch
{
    [HarmonyPostfix]
    private static void Postfix(Player registeredPlayer, bool __result)
    {
        if (__result && ModInformation.IsServer &&
            ContainerProvider.TryResolve<LordWantsRivalCapturedFinalizationProofStrategy>(out var strategy))
        {
            strategy.ClearPlayerPendingCaptures(registeredPlayer.ControllerId);
        }
    }
}
