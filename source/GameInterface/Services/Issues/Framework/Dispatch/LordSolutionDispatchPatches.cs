using Common;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Framework.AcceptCoordination;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Framework.Dispatch;

[HarmonyPatch]
internal class LordSolutionDispatchPatches
{
    [HarmonyPatch(typeof(IssueBase), nameof(IssueBase.StartIssueWithLordSolution))]
    [HarmonyPrefix]
    private static bool StartPrefix(IssueBase __instance) => Before(__instance, LordSolutionOperation.Start);

    [HarmonyPatch(typeof(IssueBase), nameof(IssueBase.CompleteIssueWithLordSolutionWithAcceptCounterOffer))]
    [HarmonyPrefix]
    private static bool AcceptPrefix(IssueBase __instance) => Before(__instance, LordSolutionOperation.AcceptCounterOffer);

    [HarmonyPatch(typeof(IssueBase), nameof(IssueBase.CompleteIssueWithLordSolutionWithRefuseCounterOffer))]
    [HarmonyPrefix]
    private static bool RefusePrefix(IssueBase __instance) => Before(__instance, LordSolutionOperation.RefuseCounterOffer);

    [HarmonyPatch(typeof(IssueBase), nameof(IssueBase.BeforeGameMenuOpened))]
    [HarmonyPrefix]
    private static bool BeforeMenuPrefix(IssueBase __instance)
    {
        if (!IsRegistered(__instance)) return true;
        return ModInformation.IsClient && IsLocalOwner(__instance) &&
            !(ContainerProvider.TryResolve<ILordSolutionCoordinator>(out var coordinator) && coordinator.IsDecisionPending(__instance));
    }

    [HarmonyPatch(typeof(IssuesCampaignBehavior), nameof(IssuesCampaignBehavior.GetCounterOfferersIssue))]
    [HarmonyPostfix]
    private static void CounterOfferPostfix(ref IssueBase __result)
    {
        if (__result == null || !IsRegistered(__result) || IsLocalOwner(__result)) return;
        __result = Campaign.Current.IssueManager.Issues.Values.FirstOrDefault(issue =>
            issue.IsSolvingWithLordSolution && issue.CounterOfferHero == Hero.OneToOneConversationHero && IsLocalOwner(issue));
    }

    private static bool Before(IssueBase issue, LordSolutionOperation operation)
    {
        return !IsRegistered(issue) || !ContainerProvider.TryResolve<ILordSolutionCoordinator>(out var coordinator) ||
            coordinator.Before(issue, operation);
    }

    private static bool IsRegistered(IssueBase issue) =>
        ContainerProvider.TryResolve<IQuestTypeRegistry>(out var registry) && registry.IsRegistered(issue.GetType());

    private static bool IsLocalOwner(IssueBase issue) =>
        ContainerProvider.TryResolve<IIssueOwnerResolver>(out var resolver) &&
        ContainerProvider.TryResolve<IControllerIdProvider>(out var controller) &&
        resolver.TryGetOwnerControllerId(issue, out var owner) && owner == controller.ControllerId;
}
