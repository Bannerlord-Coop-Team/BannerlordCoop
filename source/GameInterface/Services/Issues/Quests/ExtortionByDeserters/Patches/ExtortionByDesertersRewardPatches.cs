using Common;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;
using Quest = TaleWorlds.CampaignSystem.Issues.ExtortionByDesertersIssueBehavior.ExtortionByDesertersIssueQuest;

namespace GameInterface.Services.Issues.Quests.ExtortionByDeserters.Patches;

/// <summary>Routes the three inline dialogue rewards before any personal or world consequence runs.</summary>
[HarmonyPatch]
internal class ExtortionByDesertersRewardPatches
{
    internal static bool ApplyQuestResultPrefix() => ModInformation.IsServer;

    [HarmonyPatch(typeof(Quest), nameof(Quest.QuestCompletionDialogFlow), MethodType.Getter)]
    [HarmonyPostfix]
    private static void RewardDialogPostfix(Quest __instance, DialogFlow __result)
    {
        var choices = __result.Lines.Where(line => line.ConsequenceDelegate != null).ToArray();
        for (var i = 0; i < choices.Length; i++)
        {
            var proof = (byte)((byte)ExtortionByDesertersBranch.FullReward + i);
            var consequence = choices[i].ConsequenceDelegate;
            choices[i].ConsequenceDelegate = () =>
            {
                if (ContainerProvider.TryResolve<IOutcomeDispatcher>(out var dispatcher) &&
                    dispatcher.BeforeQuestBranch(__instance, proof)) consequence();
            };
        }
    }

    [HarmonyPatch(typeof(QuestBase), nameof(QuestBase.AddLog), new[] { typeof(TextObject), typeof(bool) })]
    [HarmonyPrefix]
    private static void AddLogPrefix(QuestBase __instance, TextObject text)
    {
        if (ModInformation.IsServer && __instance is Quest quest &&
            ContainerProvider.TryResolve<ExtortionByDesertersSync>(out var sync))
        {
            sync.Send(quest, text.ToString());
        }
    }

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnTimedOut))]
    [HarmonyPrefix]
    private static bool OnTimedOutPrefix() => ModInformation.IsServer;
}
