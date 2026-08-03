using Common;
using Common.Messaging;
using GameInterface.Services.Issues.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Patches;

/// <summary>
/// Captures + broadcasts the result of a genuine server-side <c>IssueManager.CreateNewIssue</c> creating a
/// <see cref="VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssue"/>, so every client
/// replicates the exact same rolled requested material instead of independently re-rolling it - see
/// <see cref="Interfaces.VillageNeedsCraftingMaterialsIssueInterface.ConstructReplicated"/>.
///
/// Deliberately its own, independent postfix-only class rather than a change to
/// <see cref="IssueManagerCreateNewIssuePatches"/>: that type's <c>Prefix</c> (client-creation blocking) is
/// already fully generic - it gates ANY <c>IssueManager.CreateNewIssue</c> call regardless of issue type, so it
/// already covers this issue type's creation with zero changes. Only a second postfix is needed to also
/// capture/broadcast THIS type's result; Harmony runs multiple independent postfixes on the same method
/// without conflict.
/// </summary>
[HarmonyPatch(typeof(IssueManager))]
internal class VillageNeedsCraftingMaterialsIssueCreationPatch
{
    [HarmonyPatch(nameof(IssueManager.CreateNewIssue))]
    [HarmonyPostfix]
    private static void Postfix(Hero issueOwner, bool __result)
    {
        // Only broadcast a genuine server-side creation - a replayed creation on a client also reaches this
        // postfix (Harmony runs postfixes regardless of which branch let the prefix through), so gate on
        // ModInformation instead of CallOriginalPolicy, same reasoning as IssueManagerCreateNewIssuePatches.
        if (!__result || ModInformation.IsClient) return;
        if (issueOwner?.Issue is not VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssue villageIssue) return;

        MessageBroker.Instance.Publish(issueOwner, new VillageCraftingIssueCreated(villageIssue));
    }
}
