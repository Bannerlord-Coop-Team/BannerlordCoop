using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.SaveSystem;

namespace GameInterface.Services.Issues.Quests.HeadmanNeedsGrain.Patches;

public sealed class HeadmanNeedsGrainSaveableTypeDefiner : SaveableTypeDefiner
{
    public HeadmanNeedsGrainSaveableTypeDefiner() : base(44_195_000)
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
