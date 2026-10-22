using Common;
using Common.Messaging;
using GameInterface.Services.Issues.Framework.Finalization;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Framework.Dispatch;

/// <summary>Captures branch and completion-callback logs before vanilla detaches the quest.</summary>
[HarmonyPatch(typeof(IssueBase), nameof(IssueBase.IssueFinalized))]
internal class IssueFinalizedJournalPatch
{
    [HarmonyPrefix]
    private static void Prefix(IssueBase __instance)
    {
        if (ModInformation.IsServer && ContainerProvider.TryResolve<IQuestTypeRegistry>(out var registry) &&
            registry.IsRegistered(__instance.GetType()))
            MessageBroker.Instance.Publish(__instance, new IssueJournalObserved(__instance));
    }
}
