using Common;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.Issues.Framework.AcceptCoordination;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Framework.Dispatch;

[HarmonyPatch(typeof(IssueBase), nameof(IssueBase.StartIssueWithAlternativeSolution))]
internal class StartIssueWithAlternativeSolutionDispatchPatch
{
    [HarmonyPrefix]
    private static bool Prefix(IssueBase __instance)
    {
        if (ModInformation.IsServer)
        {
            return true;
        }

        if (CallOriginalPolicy.IsOriginalAllowed())
        {
            return true;
        }

        if (!ContainerProvider.TryResolve<IQuestTypeRegistry>(out var registry))
        {
            return true;
        }

        if (!registry.TryGet(__instance.GetType(), out var descriptor))
        {
            return true;
        }

        if (descriptor.AlternativeSolutionAcceptStrategy == null)
        {
            return true;
        }

        // The server runs the start, this client gets the result back with everyone else
        MessageBroker.Instance.Publish(__instance, new AlternativeSolutionAcceptRequested(__instance));
        return false;
    }
}
