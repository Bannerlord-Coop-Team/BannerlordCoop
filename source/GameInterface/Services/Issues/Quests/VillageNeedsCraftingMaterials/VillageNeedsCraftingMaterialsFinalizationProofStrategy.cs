using Common.Logging;
using GameInterface.Services.Issues.Framework.Interface;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using Quest = TaleWorlds.CampaignSystem.Issues.VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssueQuest;

namespace GameInterface.Services.Issues.Quests.VillageNeedsCraftingMaterials;

internal class VillageNeedsCraftingMaterialsFinalizationProofStrategy : IFinalizationProofStrategy
{
    private static readonly ILogger Logger = LogManager.GetLogger<VillageNeedsCraftingMaterialsFinalizationProofStrategy>();

    public bool TryRunBranch(QuestBase quest, byte proof)
    {
        if (!(quest is Quest craftingQuest) || (VillageNeedsCraftingMaterialsBranch)proof != VillageNeedsCraftingMaterialsBranch.SuccessByHandingOverMaterials)
        {
            return false;
        }

        var village = craftingQuest.QuestGiver.CurrentSettlement;

        if (MobileParty.MainParty.CurrentSettlement != village)
        {
            Logger.Warning("The owner of {quest} is not in {settlement}", quest.StringId, village?.StringId);
            return true;
        }

        if (PartyBase.MainParty.ItemRoster.GetItemNumber(craftingQuest._requestedItem) < craftingQuest._requestedItemAmount)
        {
            Logger.Warning("The owner of {quest} does not carry the requested materials", quest.StringId);
            return true;
        }

        HandOverMaterials(craftingQuest, village.Party);
        return true;
    }

    // Vanilla Success() hands the materials to Settlement.CurrentSettlement, which on the server reads the campaign wide
    // encounter instead of the owner's party, so its body runs here against the giver's village
    private static void HandOverMaterials(Quest quest, PartyBase village)
    {
        quest.AddLog(quest.QuestSuccessLogText);

        var materials = new ItemRosterElement(quest._requestedItem, quest._requestedItemAmount);
        GiveItemAction.ApplyForParties(PartyBase.MainParty, village, in materials);
        GiveGoldAction.ApplyBetweenCharacters(null, Hero.MainHero, quest.RewardGold);
        TraitLevelingHelper.OnIssueSolvedThroughQuest(Hero.MainHero, new[]
        {
            new Tuple<TraitObject, int>(DefaultTraits.Honor, Quest.SuccessHonorBonus)
        });

        quest.QuestGiver.AddPower(Quest.SuccessPowerBonus);
        quest.RelationshipChangeWithQuestGiver = Quest.SuccessRelationBonus;
        village.Settlement.Village.Hearth += Quest.SuccessHearthBonus;
        quest.CompleteQuestWithSuccess();
    }
}
