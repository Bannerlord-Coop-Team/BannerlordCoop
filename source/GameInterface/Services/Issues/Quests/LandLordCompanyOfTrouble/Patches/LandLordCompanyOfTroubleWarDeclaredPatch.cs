using Common;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using Quest = TaleWorlds.CampaignSystem.Issues.LandLordCompanyOfTroubleIssueBehavior.LandLordCompanyOfTroubleIssueQuest;

namespace GameInterface.Services.Issues.Quests.LandLordCompanyOfTrouble.Patches;

[HarmonyPatch]
internal class LandLordCompanyOfTroubleWarDeclaredPatch
{
    [HarmonyPatch(typeof(DeclareWarAction), nameof(DeclareWarAction.ApplyInternal))]
    [HarmonyPostfix]
    private static void ApplyInternalPostfix(IFaction faction1, IFaction faction2, DeclareWarAction.DeclareWarDetail declareWarDetail)
    {
        if (!ContainerProvider.TryResolve<IOutcomeDispatcher>(out var dispatcher))
        {
            return;
        }

        dispatcher.RunAsOwners<Quest>(quest => quest.OnWarDeclared(faction1, faction2, declareWarDetail));
    }

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnWarDeclared))]
    [HarmonyPrefix]
    private static bool OnWarDeclaredPrefix()
    {
        return ModInformation.IsServer;
    }
}
