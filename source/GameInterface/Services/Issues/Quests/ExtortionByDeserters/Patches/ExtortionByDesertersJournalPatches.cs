using Common;
using GameInterface.AutoSync;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;
using Quest = TaleWorlds.CampaignSystem.Issues.ExtortionByDesertersIssueBehavior.ExtortionByDesertersIssueQuest;

namespace GameInterface.Services.Issues.Quests.ExtortionByDeserters.Patches;

/// <summary>Forwards server journal entries and quest state to the owner.</summary>
internal class ExtortionByDesertersJournalPatches
{
    internal static void AddPatches(IAutoSyncPatchCollector collector)
    {
        var target = AccessTools.Method(typeof(QuestBase), nameof(QuestBase.AddLog), new[] { typeof(TextObject), typeof(bool) });
        collector.AddPrefix(target, AccessTools.Method(typeof(ExtortionByDesertersJournalPatches), nameof(AddLogPrefix)));
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
