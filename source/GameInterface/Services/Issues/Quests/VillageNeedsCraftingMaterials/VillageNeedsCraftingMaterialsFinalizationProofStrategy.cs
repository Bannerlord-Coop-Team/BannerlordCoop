using Common.Logging;
using GameInterface.Services.Issues.Framework.Interface;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
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

        // Vanilla hands the materials to the settlement the owner is in
        if (Settlement.CurrentSettlement != craftingQuest.QuestGiver.CurrentSettlement)
        {
            Logger.Warning("The owner of {quest} is not in {settlement}", quest.StringId, craftingQuest.QuestGiver.CurrentSettlement?.StringId);
            return true;
        }

        if (PartyBase.MainParty.ItemRoster.GetItemNumber(craftingQuest._requestedItem) < craftingQuest._requestedItemAmount)
        {
            Logger.Warning("The owner of {quest} does not carry the requested materials", quest.StringId);
            return true;
        }

        craftingQuest.Success();
        return true;
    }
}
