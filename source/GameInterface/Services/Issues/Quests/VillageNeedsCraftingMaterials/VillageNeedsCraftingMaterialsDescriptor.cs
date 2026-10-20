using GameInterface.Services.Issues.Framework.Interface;
using System;
using Issue = TaleWorlds.CampaignSystem.Issues.VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssue;

namespace GameInterface.Services.Issues.Quests.VillageNeedsCraftingMaterials;

internal class VillageNeedsCraftingMaterialsDescriptor : IQuestTypeDescriptor
{
    public Type IssueType => typeof(Issue);

    public ICreationCaptureStrategy CreationCaptureStrategy { get; }

    public IQuestSolutionAcceptStrategy QuestSolutionAcceptStrategy { get; }

    public IAlternativeSolutionAcceptStrategy AlternativeSolutionAcceptStrategy { get; }

    public IFinalizationProofStrategy FinalizationProofStrategy { get; }
    public bool SuccessOnlyThroughBranchProof => false;

    public VillageNeedsCraftingMaterialsDescriptor(
        VillageNeedsCraftingMaterialsCreationCaptureStrategy creationCaptureStrategy,
        VillageNeedsCraftingMaterialsQuestSolutionAcceptStrategy questSolutionAcceptStrategy,
        VillageNeedsCraftingMaterialsAlternativeSolutionAcceptStrategy alternativeSolutionAcceptStrategy,
        VillageNeedsCraftingMaterialsFinalizationProofStrategy finalizationProofStrategy)
    {
        CreationCaptureStrategy = creationCaptureStrategy;
        QuestSolutionAcceptStrategy = questSolutionAcceptStrategy;
        AlternativeSolutionAcceptStrategy = alternativeSolutionAcceptStrategy;
        FinalizationProofStrategy = finalizationProofStrategy;
    }
}
