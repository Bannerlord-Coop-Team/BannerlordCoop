using GameInterface.Services.Issues.Quests.EscortMerchantCaravan;
using System.Runtime.Serialization;
using Xunit;
using Issue = TaleWorlds.CampaignSystem.Issues.EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssue;

namespace GameInterface.Tests.Services.Issues;

public class EscortCreationCaptureTests
{
    [Theory]
    [InlineData(3)]
    [InlineData(9)]
    public void CreationCapturePreservesTheServersCompanionRewardRoll(int rewardRoll)
    {
        var strategy = new EscortCreationCaptureStrategy();
        var server = (Issue)FormatterServices.GetUninitializedObject(typeof(Issue));
        var mirror = (Issue)FormatterServices.GetUninitializedObject(typeof(Issue));
        server._companionRewardRandom = rewardRoll;
        mirror._companionRewardRandom = 5;

        strategy.ApplyAfterCreation(mirror, strategy.Capture(server));

        Assert.Equal(rewardRoll, mirror._companionRewardRandom);
    }
}
