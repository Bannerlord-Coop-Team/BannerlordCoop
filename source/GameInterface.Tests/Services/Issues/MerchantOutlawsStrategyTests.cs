using GameInterface.Services.Issues.Quests.MerchantNeedsHelpWithOutlaws;
using GameInterface.Services.ObjectManager;
using Moq;
using System.IO;
using System.Runtime.Serialization;
using System.Text;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit;
using Quest = TaleWorlds.CampaignSystem.Issues.MerchantNeedsHelpWithOutlawsIssueQuestBehavior.MerchantNeedsHelpWithOutlawsIssueQuest;

namespace GameInterface.Tests.Services.Issues;

public class MerchantOutlawsStrategyTests
{
    [Fact]
    public void MissingHideoutRefusesCreationInsteadOfConstructingABrokenIssue()
    {
        var objects = new Mock<IObjectManager>();
        Settlement missing = null!;
        objects.Setup(manager => manager.TryGetObjectWithLogging("hideout_missing", out missing)).Returns(false);
        var strategy = new MerchantOutlawsCreationCaptureStrategy(objects.Object);

        Assert.Throws<InvalidDataException>(() => strategy.CreateIssue(null!, Encoding.UTF8.GetBytes("hideout_missing")));
        objects.Verify(manager => manager.TryGetObjectWithLogging("hideout_missing", out missing), Times.Once);
    }

    [Fact]
    public void UnrelatedQuestOrProofCannotRunTheSuccessBranch()
    {
        var strategy = new MerchantOutlawsFinalizationProofStrategy();
        Assert.False(strategy.TryRunBranch(null!, MerchantOutlawsFinalizationProofStrategy.Success));
        var quest = (Quest)FormatterServices.GetUninitializedObject(typeof(Quest));
        Assert.False(strategy.TryRunBranch(quest, byte.MaxValue));
    }
}
