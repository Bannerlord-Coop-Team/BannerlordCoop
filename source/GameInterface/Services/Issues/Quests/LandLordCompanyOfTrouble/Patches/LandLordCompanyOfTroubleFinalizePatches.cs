using Common;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;
using Quest = TaleWorlds.CampaignSystem.Issues.LandLordCompanyOfTroubleIssueBehavior.LandLordCompanyOfTroubleIssueQuest;

namespace GameInterface.Services.Issues.Quests.LandLordCompanyOfTrouble.Patches;

[HarmonyPatch]
internal class LandLordCompanyOfTroubleFinalizePatches
{
    [HarmonyPatch(typeof(Quest), nameof(Quest.OnFinalize))]
    [HarmonyPrefix]
    private static bool OnFinalizePrefix()
    {
        return ModInformation.IsServer;
    }

    [HarmonyPatch(typeof(IssueBase), nameof(IssueBase.IssueFinalized))]
    [HarmonyPrefix]
    private static void IssueFinalizedPrefix(IssueBase __instance)
    {
        if (ModInformation.IsClient || !(__instance.IssueQuest is Quest quest) || !quest.IsOngoing)
        {
            return;
        }

        if (!ContainerProvider.TryResolve<IOutcomeDispatcher>(out var dispatcher))
        {
            return;
        }

        dispatcher.RunAsOwners<Quest>(ownedQuest =>
        {
            if (ownedQuest == quest)
            {
                ownedQuest.OnFinalize();
            }
        });
    }
}
