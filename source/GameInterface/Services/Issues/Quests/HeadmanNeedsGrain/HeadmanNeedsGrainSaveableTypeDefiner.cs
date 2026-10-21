using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.SaveSystem;

namespace GameInterface.Services.Issues.Quests.HeadmanNeedsGrain;

public sealed class HeadmanNeedsGrainSaveableTypeDefiner : SaveableTypeDefiner
{
    private const int SaveBaseId = 44_195_000;

    public HeadmanNeedsGrainSaveableTypeDefiner() : base(SaveBaseId)
    {
    }

    public override void DefineClassTypes()
    {
    }

    public override void DefineContainerDefinitions()
    {
        ConstructContainerDefinition(typeof(Dictionary<HeadmanNeedsGrainIssueBehavior.HeadmanNeedsGrainIssue, Settlement>));
        ConstructContainerDefinition(typeof(Dictionary<Hero, Dictionary<string, int>>));
    }
}
