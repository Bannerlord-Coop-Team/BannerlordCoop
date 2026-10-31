using Common;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;
using Quest = TaleWorlds.CampaignSystem.Issues.LandLordCompanyOfTroubleIssueBehavior.LandLordCompanyOfTroubleIssueQuest;

namespace GameInterface.Services.Issues.Quests.LandLordCompanyOfTrouble.Patches;

[HarmonyPatch]
[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
internal class LandLordCompanyOfTroubleFinalizePatches
{
    [HarmonyPatch(typeof(Quest), nameof(Quest.OnFinalize))]
    [HarmonyPrefix]
    private static bool OnFinalizePrefix()
    {
        return ModInformation.IsServer;
    }

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnFinalize))]
    [HarmonyPostfix]
    private static void OnFinalizePostfix(Quest __instance)
    {
        if (ModInformation.IsClient || !ContainerProvider.TryResolve<ILandLordCompanyOfTroubleService>(out var service))
        {
            return;
        }

        service.RemoveCompanyOf(__instance);
    }

    [HarmonyPatch(typeof(IssueBase), nameof(IssueBase.IssueFinalized))]
    [HarmonyPrefix]
    private static void IssueFinalizedPrefix(IssueBase __instance)
    {
        if (ModInformation.IsClient || !ContainerProvider.TryResolve<ILandLordCompanyOfTroubleService>(out var service))
        {
            return;
        }

        service.FinalizeIssue(__instance);
    }
}
