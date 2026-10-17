using GameInterface.Services.Issues.Framework.Interface;
using System;
using Common;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Quests.LadysKnightOut;

internal class LadysKnightOutQuestSolutionAcceptStrategy : IQuestSolutionAcceptStrategy
{
    private readonly ILadysKnightOutHonorProgress honor;

    public LadysKnightOutQuestSolutionAcceptStrategy(ILadysKnightOutHonorProgress honor) { this.honor = honor; }

    public byte[] Capture(IssueBase issue)
    {
        var xp = Campaign.Current.PlayerTraitDeveloper?.GetPropertyValue(DefaultTraits.Honor) ??
            Campaign.Current.Models.CharacterDevelopmentModel.GetTraitXpRequiredForTraitLevel(DefaultTraits.Honor, Hero.MainHero.GetTraitLevel(DefaultTraits.Honor));
        return BitConverter.GetBytes(xp);
    }

    public void Apply(IssueBase issue, byte[] captured)
    {
        if (ModInformation.IsServer && captured.Length == sizeof(int)) honor.Seed(issue, BitConverter.ToInt32(captured, 0));
    }
}
