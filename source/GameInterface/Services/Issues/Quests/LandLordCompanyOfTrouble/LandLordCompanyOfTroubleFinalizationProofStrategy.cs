using Common.Logging;
using GameInterface.Services.Issues.Framework.Interface;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using Quest = TaleWorlds.CampaignSystem.Issues.LandLordCompanyOfTroubleIssueBehavior.LandLordCompanyOfTroubleIssueQuest;

namespace GameInterface.Services.Issues.Quests.LandLordCompanyOfTrouble;

internal class LandLordCompanyOfTroubleFinalizationProofStrategy : IFinalizationProofStrategy
{
    private static readonly ILogger Logger = LogManager.GetLogger<LandLordCompanyOfTroubleFinalizationProofStrategy>();

    public bool TryRunBranch(QuestBase quest, byte proof)
    {
        if (!(quest is Quest companyQuest))
        {
            return false;
        }

        switch ((LandLordCompanyOfTroubleBranch)proof)
        {
            case LandLordCompanyOfTroubleBranch.SuccessBySellingCompany:
                SellCompany(companyQuest);
                return true;
            case LandLordCompanyOfTroubleBranch.FailByCompanyLeaving:
                LetCompanyLeave(companyQuest);
                return true;
            case LandLordCompanyOfTroubleBranch.CompanyTurnedHostile:
                TurnCompanyHostile(companyQuest);
                return true;
            default:
                return false;
        }
    }

    private static void SellCompany(Quest quest)
    {
        quest.UpdateCompanyTroopCount();
        quest._demandGold = 1000 + (quest._companyTroopCount * 150);
        quest.persuasion_complete_with_company_of_trouble_on_consequence();
    }

    private static void LetCompanyLeave(Quest quest)
    {
        quest.RelationshipChangeWithQuestGiver = -2;
        quest.UpdateCompanyTroopCount();
        MobileParty.MainParty.MemberRoster.AddToCounts(quest._troubleCharacterObject, -quest._companyTroopCount);
        quest.AddLog(quest.QuestFailCompanyLeft);
        quest.CompleteQuestWithFail();
    }

    private static void TurnCompanyHostile(Quest quest)
    {
        if (quest._companyOfTroubleParty != null)
        {
            Logger.Warning("The mercenaries of {quest} already turned hostile", quest.StringId);
            return;
        }

        quest.UpdateCompanyTroopCount();
        if (quest._companyTroopCount == 0)
        {
            Logger.Warning("The owner of {quest} has no mercenaries left to turn hostile", quest.StringId);
            return;
        }

        quest.CreateCompanyEnemyParty();

        SetPartyAiAction.GetActionForEngagingParty(quest._companyOfTroubleParty, MobileParty.MainParty, MobileParty.NavigationType.Default, isFromPort: false);
        quest._companyOfTroubleParty.Ai.SetDoNotMakeNewDecisions(true);
    }
}
