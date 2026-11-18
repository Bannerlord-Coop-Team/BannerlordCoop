using Common;
using Common.Util;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Issues.Quests.ExtortionByDeserters;
using HarmonyLib;
using Helpers;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using Quest = TaleWorlds.CampaignSystem.Issues.ExtortionByDesertersIssueBehavior.ExtortionByDesertersIssueQuest;

namespace GameInterface.Services.Issues.Quests.ExtortionByDeserters.Patches;

/// <summary>World creation and cleanup belong to the server, encounters to the owning client.</summary>
[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
[HarmonyPatch]
internal class ExtortionByDesertersPartyPatches
{
    [HarmonyPatch(typeof(QuestBase), nameof(QuestBase.AddTrackedObject))]
    [HarmonyPrefix]
    private static bool AddTrackedObjectPrefix(QuestBase __instance, ITrackableCampaignObject trackedObject)
    {
        // Its constructor runs before the server's party binding arrives.
        return !(__instance is Quest) || trackedObject != null;
    }

    [HarmonyPatch(typeof(Quest), nameof(Quest.StartAmbushEncounter))]
    [HarmonyPrefix]
    private static bool StartAmbushEncounterPrefix() => false;

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnBattleStarted))]
    [HarmonyPrefix]
    private static bool OnBattleStartedPrefix() => false;

    // The server cleans up the battle parties in its FinalizeEventAux postfix, the only caller of OnMapEventEnded
    [HarmonyPatch(typeof(Quest), nameof(Quest.GameMenuOpened))]
    [HarmonyPrefix]
    private static bool GameMenuOpenedPrefix() => false;

    // Dismissing an issue drops its running quest unfinished, which would leave the deserters and defenders on the map
    [HarmonyPatch(typeof(IssueBase), nameof(IssueBase.IssueFinalized))]
    [HarmonyPrefix]
    private static void IssueFinalizedPrefix(IssueBase __instance)
    {
        if (ModInformation.IsServer && __instance.IssueQuest is Quest quest && quest.IsOngoing) quest.OnFinalize();
    }

    [HarmonyPatch(typeof(QuestBase), nameof(QuestBase.AddLog), new[] { typeof(TextObject), typeof(bool) })]
    [HarmonyPrefix]
    private static void AddLogPrefix(QuestBase __instance, TextObject text)
    {
        if (ModInformation.IsServer && __instance is Quest quest &&
            ContainerProvider.TryResolve<IExtortionByDesertersProgress>(out var progress))
        {
            progress.Send(quest, text);
        }
    }
}

/// <summary>These world methods must stay server-only even during client replay.</summary>
[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
[HarmonyPatch]
internal class ExtortionByDesertersServerMethodsPatch
{
    internal static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(Quest), nameof(Quest.CreateDeserterParty));
        yield return AccessTools.Method(typeof(Quest), nameof(Quest.CreateDefenderParty));
        yield return AccessTools.Method(typeof(Quest), nameof(Quest.ReleaseDeserterParty));
        yield return AccessTools.Method(typeof(Quest), nameof(Quest.ApplyQuestResult));
        yield return AccessTools.Method(typeof(Quest), nameof(Quest.OnTimedOut));
        yield return AccessTools.Method(typeof(Quest), nameof(Quest.DestroyDeserterParty));
        yield return AccessTools.Method(typeof(Quest), nameof(Quest.DestroyDefenderParty));
        yield return AccessTools.Method(typeof(Quest), nameof(Quest.HourlyTick));
        yield return AccessTools.Method(typeof(Quest), nameof(Quest.GameMenuOptionSelected));
        yield return AccessTools.Method(typeof(Quest), nameof(Quest.OnWarDeclared));
        yield return AccessTools.Method(typeof(Quest), nameof(Quest.OnClanChangedKingdom));
    }

    [HarmonyPrefix]
    internal static bool Prefix() => ModInformation.IsServer;
}
