using Common;
using Common.Messaging;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Framework.Dismissal;

/// <summary>
/// The only patch on the troop return. On the server it prepares it, lets a quest replace it, then tells the quests.
/// </summary>
[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
[HarmonyPatch(typeof(IssueManager), nameof(IssueManager.TryToMakeTroopsReturn))]
internal class AlternativeTroopsReturnPatch
{
    [HarmonyPrefix]
    private static bool Prefix(IssueBase issue)
    {
        if (ModInformation.IsClient || !ContainerProvider.TryResolve<IIssueDismisser>(out var issueDismisser) ||
            !ContainerProvider.TryResolve<IQuestTypeRegistry>(out var registry)) return true;

        issueDismisser.PrepareTroopReturn(issue);
        if (!registry.TryGet(issue.GetType(), out var descriptor) || descriptor is not ITroopReturnDescriptor quest) return true;

        return !quest.ReturnTroops(issue);
    }

    [HarmonyPostfix]
    private static void Postfix(IssueBase issue)
    {
        if (ModInformation.IsServer) MessageBroker.Instance.Publish(issue, new AlternativeTroopsReturned(issue));
    }
}
