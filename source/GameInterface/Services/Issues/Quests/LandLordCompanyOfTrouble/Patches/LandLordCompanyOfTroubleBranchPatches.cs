using Common;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using Quest = TaleWorlds.CampaignSystem.Issues.LandLordCompanyOfTroubleIssueBehavior.LandLordCompanyOfTroubleIssueQuest;

namespace GameInterface.Services.Issues.Quests.LandLordCompanyOfTrouble.Patches;

[HarmonyPatch]
[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
internal class LandLordCompanyOfTroubleBranchPatches
{
    // The lines kept are vanilla's UI part, closing the encounter and the persuasion, the server pays and ends the quest
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

    // The lines kept are vanilla's UI part, the server removes the mercenaries and fails the quest
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

    // The server spawns the company from the owner's mercenaries, the client leaves the company menu because the fight now happens on the map
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

    // The server takes the food from the owner's roster, the owner's client shows vanilla's notice and counts the theft
    [HarmonyPatch(typeof(Quest), nameof(Quest.TryToStealItemFromPlayer))]
    [HarmonyPrefix]
    private static bool FoodStolenPrefix(Quest __instance)
    {
        if (ModInformation.IsServer)
        {
            return true;
        }

        if (MobileParty.MainParty.ItemRoster.TotalFood <= 0)
        {
            return false;
        }

        // The mercenaries left the party when they turned hostile, so nobody is left to steal
        if (!MobileParty.MainParty.MemberRoster.Contains(__instance._troubleCharacterObject))
        {
            return false;
        }

        if (BeforeBranch(__instance, LandLordCompanyOfTroubleBranch.FoodStolen))
        {
            return true;
        }

        ShowTheft(__instance);
        __instance._thieveryCount++;
        return false;
    }

    private static void ShowTheft(Quest quest)
    {
        if (quest._thieveryCount > 1)
        {
            MBInformationManager.AddQuickInformation(new TextObject("{=xlm8oYhM}Your men reported that some of the goods were stolen from the baggage train."));
            return;
        }

        var text = quest._thieveryCount == 0
            ? new TextObject("{=OKpwA8Az}Your men have noticed some of the goods in the baggage train are missing.")
            : new TextObject("{=acu1wTeq}Your men are sure of that some of the goods were stolen from the baggage train.");

        InformationManager.ShowInquiry(
            new InquiryData(quest.Title.ToString(), text.ToString(), isAffirmativeOptionShown: true, isNegativeOptionShown: false, GameTexts.FindText("str_ok").ToString(), "", null, null),
            pauseGameActiveState: true);
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
