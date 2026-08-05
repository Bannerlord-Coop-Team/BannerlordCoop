using GameInterface.Services.Issues.Interfaces;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Localization;

namespace GameInterface.Services.Issues.Patches;

/// <summary>
/// Same bug/fix shape as <see cref="VillageNeedsToolsQuestOwnershipGatePatch"/> (see that type's doc comment
/// for the full derivation), targeting the DIFFERENT method signature this quest type actually uses for its
/// turn-in dialogue's clickable condition: <c>CompleteQuestClickableConditions(out TextObject)</c>, not Tools'
/// parameterless <c>PlayerHasTools()</c>. Gates the condition itself (cheap, local, already-synced ownership
/// check - see <see cref="VillageNeedsToolsIssueOwnership"/>, reused unchanged for this issue type too), so a
/// non-owner's "Yes, I have them with me." option becomes permanently unsatisfiable, making
/// <c>Success()</c> (which deducts items and pays gold) unreachable for anyone except the recorded owner.
/// </summary>
[HarmonyPatch(typeof(VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssueQuest), "CompleteQuestClickableConditions")]
internal class VillageNeedsCraftingMaterialsQuestOwnershipGatePatch
{
    [HarmonyPrefix]
    private static bool Prefix(VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssueQuest __instance, ref bool __result, out TextObject explanation)
    {
        if (!VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(__instance.QuestGiver))
        {
            __result = false;
            explanation = new TextObject("{=!}You don't have enough crafting materials.");
            return false; // skip the original - non-owners never see/can-select the option
        }

        explanation = null; // definite-assignment only - the original computes the real explanation
        return true; // real owner: run the real check unmodified
    }
}
