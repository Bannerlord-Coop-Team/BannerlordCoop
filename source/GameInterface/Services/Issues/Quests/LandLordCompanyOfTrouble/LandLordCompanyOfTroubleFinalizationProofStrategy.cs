using Common.Logging;
using GameInterface.Services.Issues.Framework.Interface;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using Quest = TaleWorlds.CampaignSystem.Issues.LandLordCompanyOfTroubleIssueBehavior.LandLordCompanyOfTroubleIssueQuest;

namespace GameInterface.Services.Issues.Quests.LandLordCompanyOfTrouble;

internal class LandLordCompanyOfTroubleFinalizationProofStrategy : IFinalizationProofStrategy, IOwnerPartyRequiredFinalizationProofStrategy
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
            case LandLordCompanyOfTroubleBranch.FoodStolen:
                StealFood(companyQuest);
                return true;
            default:
                return false;
        }
    }

    private static void SellCompany(Quest quest)
    {
        if (quest._companyOfTroubleParty != null)
        {
            Logger.Warning("The mercenaries of {quest} already turned hostile", quest.StringId);
            return;
        }

        quest.UpdateCompanyTroopCount();
        if (quest._companyTroopCount == 0)
        {
            Logger.Warning("The owner of {quest} has no mercenaries left to sell", quest.StringId);
            return;
        }

        // Vanilla's persuasion price, which the owner's client set from its own count
        quest._demandGold = 1000 + (quest._companyTroopCount * 150);
        quest.persuasion_complete_with_company_of_trouble_on_consequence();
    }

    // Vanilla's CompanyLeftQuestFail without its GameMenu.ExitToLast, which the owner's client already ran
    private static void LetCompanyLeave(Quest quest)
    {
        quest.UpdateCompanyTroopCount();
        MobileParty.MainParty.MemberRoster.AddToCounts(quest._troubleCharacterObject, -quest._companyTroopCount);
        FailAsCompanyLeft(quest);
    }

    internal static void FailAsCompanyLeft(Quest quest)
    {
        quest.RelationshipChangeWithQuestGiver = -2;
        quest.AddLog(quest.QuestFailCompanyLeft);
        quest.CompleteQuestWithFail();
    }

    // Vanilla's CreateCompanyEnemyParty, which still sets _battleWillStart, but only the client's menu reads it, so the company chases the owner on the map instead
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

    // Vanilla's TryToStealItemFromPlayer without its pop-ups, which the owner's client already showed
    private static void StealFood(Quest quest)
    {
        if (!MobileParty.MainParty.MemberRoster.Contains(quest._troubleCharacterObject))
        {
            Logger.Warning("The owner of {quest} has no mercenaries left to steal food", quest.StringId);
            return;
        }

        var roster = MobileParty.MainParty.ItemRoster;

        for (int i = 0; i < roster.Count; i++)
        {
            var element = roster[i];
            var item = element.EquipmentElement.Item;

            if (!element.IsEmpty && item.IsFood)
            {
                roster.AddToCounts(item, -1);
                quest._thieveryCount++;
                return;
            }
        }

        Logger.Warning("The owner of {quest} has no food left to steal", quest.StringId);
    }
}
