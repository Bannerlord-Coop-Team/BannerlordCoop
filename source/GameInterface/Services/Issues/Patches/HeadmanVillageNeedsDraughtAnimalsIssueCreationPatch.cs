using Common;
using Common.Messaging;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Patches;

/// <summary>
/// Captures + broadcasts a genuine server-side <c>IssueManager.CreateNewIssue</c> creating a
/// <see cref="HeadmanVillageNeedsDraughtAnimalsIssueBehavior.HeadmanVillageNeedsDraughtAnimalsIssue"/> - see
/// <see cref="IHeadmanVillageNeedsDraughtAnimalsIssueInterface"/>'s doc comment. Deliberately its own
/// independent postfix (same reasoning as <see cref="VillageNeedsCraftingMaterialsIssueCreationPatch"/>): the
/// client-creation-blocking Prefix on <see cref="IssueManagerCreateNewIssuePatches"/> is already fully generic.
/// </summary>
[HarmonyPatch(typeof(IssueManager))]
internal class HeadmanVillageNeedsDraughtAnimalsIssueCreationPatch
{
    [HarmonyPatch(nameof(IssueManager.CreateNewIssue))]
    [HarmonyPostfix]
    private static void Postfix(Hero issueOwner, bool __result)
    {
        if (!__result || ModInformation.IsClient) return;
        if (issueOwner?.Issue is not HeadmanVillageNeedsDraughtAnimalsIssueBehavior.HeadmanVillageNeedsDraughtAnimalsIssue villageIssue) return;

        MessageBroker.Instance.Publish(issueOwner, new HeadmanVillageNeedsDraughtAnimalsIssueCreated(villageIssue));
    }
}

/// <summary>
/// Bug-1-shaped fix (see <see cref="VillageNeedsToolsQuestOwnershipGatePatch"/>'s doc comment for the full
/// derivation): the <c>quest_discuss</c> "Yes, Here are your animals." player option is gated entirely by
/// <c>CheckIfPlayerHasEnoughAnimals()</c> - a check against THIS machine's OWN
/// <c>MobileParty.MainParty.ItemRoster</c>, not any owner-specific state. A non-owning peer with enough of the
/// requested animal type in their own inventory could deliver it into someone else's mirrored issue and
/// collect the real gold/meat reward + Village Hearth change. Gating this one check closes the whole
/// turn-in chain (every success branch - normal, discount-accepted, discount-declined - is only reachable
/// after this Condition passes).
/// </summary>
[HarmonyPatch(typeof(HeadmanVillageNeedsDraughtAnimalsIssueBehavior.HeadmanVillageNeedsDraughtAnimalsIssueQuest), "CheckIfPlayerHasEnoughAnimals")]
internal class HeadmanVillageNeedsDraughtAnimalsQuestOwnershipGatePatch
{
    [HarmonyPrefix]
    private static bool Prefix(HeadmanVillageNeedsDraughtAnimalsIssueBehavior.HeadmanVillageNeedsDraughtAnimalsIssueQuest __instance, ref bool __result)
    {
        if (!VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(__instance.QuestGiver))
        {
            __result = false;
            return false; // skip the original - non-owners can never appear to have enough animals
        }

        return true; // real owner: run the real check unmodified
    }
}
