using Common.Util;
using GameInterface.Services.Heroes.Commands;
using GameInterface.Tests;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using Xunit;

namespace GameInterface.Tests.Services.Heroes;

[Collection(ModInformationRoleCollection.Name)]
public class HeroDebugCommandTests
{
    [Fact]
    public void NameStartsWithPrefix_NoPrefix_Matches()
    {
        Assert.True(HeroDebugCommand.NameStartsWithPrefix("Lady Isolla", string.Empty));
    }

    [Theory]
    [InlineData("LADY", true)]
    [InlineData("lady iso", true)]
    [InlineData("Isolla", false)]
    public void NameStartsWithPrefix_FiltersCaseInsensitively(
        string prefix,
        bool expected)
    {
        Assert.Equal(expected, HeroDebugCommand.NameStartsWithPrefix("Lady Isolla", prefix));
    }

    [Fact]
    public void FormatHeroIssue_ReportsTheConcreteIssueType()
    {
        var issue = ObjectHelper.SkipConstructor<LesserNobleRevoltIssueBehavior.LesserNobleRevoltIssue>();
        issue.StringId = "issue_1";
        var hero = ObjectHelper.SkipConstructor<Hero>();
        hero.StringId = "lord_1_1";
        hero.Issue = issue;

        Assert.Equal(
            "Hero StringId: 'lord_1_1', Issue StringId: 'issue_1', " +
            "Issue Type: 'TaleWorlds.CampaignSystem.Issues.LesserNobleRevoltIssueBehavior+LesserNobleRevoltIssue'",
            HeroDebugCommand.HeroIssuesCoopCommand.FormatHeroIssue(hero));
    }
}
