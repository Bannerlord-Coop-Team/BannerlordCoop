using Common;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.AutoSync;
using GameInterface.Services.Issues.Quests.ExtortionByDeserters;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Localization;
using Quest = TaleWorlds.CampaignSystem.Issues.ExtortionByDesertersIssueBehavior.ExtortionByDesertersIssueQuest;

namespace GameInterface.Services.Issues.Quests.ExtortionByDeserters.Patches;

/// <summary>World creation and cleanup belong to the server, encounters to the owning client.</summary>
internal class ExtortionByDesertersPartyPatches
{
    internal static void AddPatches(IAutoSyncPatchCollector collector)
    {
        Add(collector, AccessTools.Method(typeof(QuestBase), nameof(QuestBase.AddTrackedObject)), nameof(AddTrackedObjectPrefix));
        Add(collector, AccessTools.Method(typeof(Quest), nameof(Quest.StartAmbushEncounter)), nameof(StartAmbushEncounterPrefix));
        Add(collector, AccessTools.Method(typeof(Quest), nameof(Quest.OnBattleStarted)), nameof(OnBattleStartedPrefix));
        Add(collector, AccessTools.Method(typeof(Quest), nameof(Quest.GameMenuOpened)), nameof(GameMenuOpenedPrefix));
        Add(collector, AccessTools.Method(typeof(IssueBase), nameof(IssueBase.IssueFinalized)), nameof(IssueFinalizedPrefix));
        Add(collector, AccessTools.Method(typeof(QuestBase), nameof(QuestBase.AddLog), new[] { typeof(TextObject), typeof(bool) }), nameof(AddLogPrefix));
    }

    private static void Add(IAutoSyncPatchCollector collector, MethodBase target, string prefixName) =>
        collector.AddPrefix(target, AccessTools.Method(typeof(ExtortionByDesertersPartyPatches), prefixName));

    [HarmonyPatch(typeof(QuestBase), nameof(QuestBase.AddTrackedObject))]
    [HarmonyPrefix]
    internal static bool AddTrackedObjectPrefix(QuestBase __instance, ITrackableCampaignObject trackedObject)
    {
        // Its constructor runs before the server's party binding arrives.
        return !(__instance is Quest) || trackedObject != null;
    }

    [HarmonyPatch(typeof(Quest), nameof(Quest.StartAmbushEncounter))]
    [HarmonyPrefix]
    internal static bool StartAmbushEncounterPrefix() => false;

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnBattleStarted))]
    [HarmonyPrefix]
    internal static bool OnBattleStartedPrefix() => false;

    // Vanilla destroys the battle parties when the encounter menu opens. The server does that in FinalizeEventAux.
    [HarmonyPatch(typeof(Quest), nameof(Quest.GameMenuOpened))]
    [HarmonyPrefix]
    internal static bool GameMenuOpenedPrefix() => false;

    // Dismissing an issue drops its running quest unfinished, which would leave the deserters and defenders on the map
    [HarmonyPatch(typeof(IssueBase), nameof(IssueBase.IssueFinalized))]
    [HarmonyPrefix]
    internal static void IssueFinalizedPrefix(IssueBase __instance)
    {
        if (ModInformation.IsServer && __instance.IssueQuest is Quest quest && quest.IsOngoing) quest.OnFinalize();
    }

    [HarmonyPatch(typeof(QuestBase), nameof(QuestBase.AddLog), new[] { typeof(TextObject), typeof(bool) })]
    [HarmonyPrefix]
    internal static void AddLogPrefix(QuestBase __instance, TextObject text)
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
