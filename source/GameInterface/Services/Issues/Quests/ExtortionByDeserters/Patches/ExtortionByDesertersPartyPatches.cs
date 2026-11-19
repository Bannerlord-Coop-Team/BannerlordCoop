using Common;
using GameInterface.AutoSync;
using HarmonyLib;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Localization;
using Quest = TaleWorlds.CampaignSystem.Issues.ExtortionByDesertersIssueBehavior.ExtortionByDesertersIssueQuest;

namespace GameInterface.Services.Issues.Quests.ExtortionByDeserters.Patches;

/// <summary>Guards early party tracking, cleans up dismissed quest parties, and forwards server journals.</summary>
internal class ExtortionByDesertersPartyPatches
{
    internal static void AddPatches(IAutoSyncPatchCollector collector)
    {
        Add(collector, AccessTools.Method(typeof(QuestBase), nameof(QuestBase.AddTrackedObject)), nameof(AddTrackedObjectPrefix));
        Add(collector, AccessTools.Method(typeof(IssueBase), nameof(IssueBase.IssueFinalized)), nameof(IssueFinalizedPrefix));
        Add(collector, AccessTools.Method(typeof(QuestBase), nameof(QuestBase.AddLog), new[] { typeof(TextObject), typeof(bool) }), nameof(AddLogPrefix));
    }

    private static void Add(IAutoSyncPatchCollector collector, MethodBase target, string prefixName) =>
        collector.AddPrefix(target, AccessTools.Method(typeof(ExtortionByDesertersPartyPatches), prefixName));

    internal static bool AddTrackedObjectPrefix(QuestBase __instance, ITrackableCampaignObject trackedObject)
    {
        // The quest constructor runs before the server's party binding arrives.
        return !(__instance is Quest) || trackedObject != null;
    }

    // Dismissing an issue drops its running quest unfinished, which would leave the deserters and defenders on the map
    internal static void IssueFinalizedPrefix(IssueBase __instance)
    {
        if (ModInformation.IsServer && __instance.IssueQuest is Quest quest && quest.IsOngoing) quest.OnFinalize();
    }

    internal static void AddLogPrefix(QuestBase __instance, TextObject text)
    {
        if (ModInformation.IsServer && __instance is Quest quest &&
            ContainerProvider.TryResolve<IExtortionByDesertersProgress>(out var progress))
        {
            progress.Send(quest, text);
        }
    }
}
