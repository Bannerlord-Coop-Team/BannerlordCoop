using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using Quest = TaleWorlds.CampaignSystem.Issues.LandLordCompanyOfTroubleIssueBehavior.LandLordCompanyOfTroubleIssueQuest;

namespace GameInterface.Services.Issues.Quests.LandLordCompanyOfTrouble.Patches;

[HarmonyPatch]
internal class LandLordCompanyOfTroubleBranchPatches
{
    [HarmonyPatch(typeof(Quest), nameof(Quest.persuasion_complete_with_company_of_trouble_on_consequence))]
    [HarmonyPrefix]
    private static bool SuccessBySellingCompanyPrefix(Quest __instance)
    {
        if (BeforeBranch(__instance, LandLordCompanyOfTroubleBranch.SuccessBySellingCompany))
        {
            return true;
        }

        if (PlayerEncounter.Current != null)
        {
            PlayerEncounter.LeaveEncounter = true;
        }

        ConversationManager.EndPersuasion();
        return false;
    }

    [HarmonyPatch(typeof(Quest), nameof(Quest.CompanyLeftQuestFail))]
    [HarmonyPrefix]
    private static bool FailByCompanyLeavingPrefix(Quest __instance)
    {
        if (BeforeBranch(__instance, LandLordCompanyOfTroubleBranch.FailByCompanyLeaving))
        {
            return true;
        }

        __instance._companyLeftQuestWillFail = false;
        GameMenu.ExitToLast();
        return false;
    }

    [HarmonyPatch(typeof(Quest), nameof(Quest.CreateCompanyEnemyParty))]
    [HarmonyPrefix]
    private static bool CompanyTurnedHostilePrefix(Quest __instance)
    {
        if (BeforeBranch(__instance, LandLordCompanyOfTroubleBranch.CompanyTurnedHostile))
        {
            return true;
        }

        if (Campaign.Current.CurrentMenuContext != null)
        {
            GameMenu.ExitToLast();
        }

        return false;
    }

    private static bool BeforeBranch(Quest quest, LandLordCompanyOfTroubleBranch branch)
    {
        if (!ContainerProvider.TryResolve<IOutcomeDispatcher>(out var dispatcher))
        {
            return true;
        }

        return dispatcher.BeforeQuestBranch(quest, (byte)branch);
    }
}
