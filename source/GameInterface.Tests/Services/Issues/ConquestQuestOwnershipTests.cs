using Common.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Framework.Registries;
using GameInterface.Services.Issues.Quests.TheConquestOfSettlement;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using Moq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using Xunit;

namespace GameInterface.Tests.Services.Issues;

public class ConquestQuestOwnershipTests
{
    [Theory]
    [InlineData("player-A", true)]
    [InlineData("player-B", false)]
    public void QuestVisibilityKeepsItsOwnerAfterIssueCleanupAndSaveRestore(string localController, bool visible)
    {
        var quest = ObjectHelper.SkipConstructor<TheConquestOfSettlementIssueBehavior.TheConquestOfSettlementIssueQuest>();
        quest.StringId = "coop_conquest_issue_31_quest";
        var controller = new Mock<IControllerIdProvider>();
        controller.SetupGet(value => value.ControllerId).Returns(localController);
        var service = new TheConquestOfSettlementService(
            Mock.Of<IObjectManager>(),
            new IssueOwnershipRegistry(),
            Mock.Of<IPlayerManager>(),
            controller.Object);
        if (visible)
        {
            service.RecordLocalQuest(quest);
        }

        Assert.Equal(visible, service.IsVisible(quest));
        Assert.Equal(visible, service.IsVisible(quest));
    }

    [Fact]
    public void MissingIssueDoesNotSubstituteAnotherParty()
    {
        var ownership = new IssueOwnershipRegistry();
        var players = new Mock<IPlayerManager>();
        var objects = new Mock<IObjectManager>();
        var service = new TheConquestOfSettlementService(
            objects.Object,
            ownership,
            players.Object,
            Mock.Of<IControllerIdProvider>());

        Assert.False(service.TryOpenOwnerScope(null, out var scope));
        Assert.Null(scope);
    }
}
