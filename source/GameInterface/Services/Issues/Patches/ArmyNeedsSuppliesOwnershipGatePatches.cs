using GameInterface.Services.Issues.Interfaces;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Patches;

/// <summary>
/// Same bug/fix shape as <see cref="VillageNeedsToolsQuestOwnershipGatePatch"/>/
/// <see cref="VillageNeedsCraftingMaterialsQuestOwnershipGatePatch"/> (see the former's doc comment for the
/// full derivation), targeting <c>ArmyNeedsSuppliesIssueQuest</c>'s FOUR separate turn-in dialogue conditions
/// (<c>CheckIfPlayerCollectedGrain</c>/<c>CheckIfPlayerCollectedGrainLiveStock</c>/
/// <c>CheckIfPlayerCollectedGrainWine</c>/<c>CheckIfPlayerCollectedEverything</c> - all four check only the
/// LOCAL <c>MobileParty.MainParty.ItemRoster</c>, with no ownership concept at all). Gates all four identically:
/// a non-owner's mirrored quest object can never satisfy ANY of the four "I have brought your X" options, no
/// matter what is in their own inventory, making every one of the <c>CollectedXConsequence</c> reward paths
/// unreachable for anyone except the recorded owner.
/// </summary>
[HarmonyPatch(typeof(ArmyNeedsSuppliesIssueBehavior.ArmyNeedsSuppliesIssueQuest))]
internal class ArmyNeedsSuppliesOwnershipGatePatches
{
    [HarmonyPatch("CheckIfPlayerCollectedGrain")]
    [HarmonyPrefix]
    private static bool CheckIfPlayerCollectedGrainPrefix(
        ArmyNeedsSuppliesIssueBehavior.ArmyNeedsSuppliesIssueQuest __instance, ref bool __result) =>
        Gate(__instance, ref __result);

    [HarmonyPatch("CheckIfPlayerCollectedGrainLiveStock")]
    [HarmonyPrefix]
    private static bool CheckIfPlayerCollectedGrainLiveStockPrefix(
        ArmyNeedsSuppliesIssueBehavior.ArmyNeedsSuppliesIssueQuest __instance, ref bool __result) =>
        Gate(__instance, ref __result);

    [HarmonyPatch("CheckIfPlayerCollectedGrainWine")]
    [HarmonyPrefix]
    private static bool CheckIfPlayerCollectedGrainWinePrefix(
        ArmyNeedsSuppliesIssueBehavior.ArmyNeedsSuppliesIssueQuest __instance, ref bool __result) =>
        Gate(__instance, ref __result);

    [HarmonyPatch("CheckIfPlayerCollectedEverything")]
    [HarmonyPrefix]
    private static bool CheckIfPlayerCollectedEverythingPrefix(
        ArmyNeedsSuppliesIssueBehavior.ArmyNeedsSuppliesIssueQuest __instance, ref bool __result) =>
        Gate(__instance, ref __result);

    private static bool Gate(ArmyNeedsSuppliesIssueBehavior.ArmyNeedsSuppliesIssueQuest instance, ref bool result)
    {
        if (!VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(instance.QuestGiver))
        {
            result = false;
            return false; // skip the original - non-owners never see/can-select any of the turn-in options
        }

        return true; // real owner: run the real check unmodified
    }
}
