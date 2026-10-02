using Common.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Generic.Migrated.TheConquestOfSettlement;
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
        var giver = ObjectHelper.SkipConstructor<Hero>();
        var quest = ObjectHelper.SkipConstructor<TheConquestOfSettlementIssueBehavior.TheConquestOfSettlementIssueQuest>();
        quest.StringId = "coop_conquest_issue_31_quest";
        var ownership = new IssueOwnershipRegistry();
        ownership.SetOwner(giver, "player-A");
        ownership.SetQuestOwner(quest.StringId, "player-A");
        ownership.Clear(giver);

        var savedOwners = ownership.SnapshotQuests();
        ownership.ClearAll();
        ownership.RestoreQuests(savedOwners);

        var controller = new Mock<IControllerIdProvider>();
        controller.SetupGet(value => value.ControllerId).Returns(localController);
        var service = new ConquestQuest(ownership, Mock.Of<IPlayerManager>(), Mock.Of<IObjectManager>(),
            Mock.Of<IIssueGenerationRegistry>(), controller.Object);

        Assert.False(ownership.TryGetOwnerControllerId(giver, out _));
        Assert.Equal(visible, service.IsVisible(quest));
        ownership.SetOwner(giver, "player-B");
        Assert.Equal(visible, service.IsVisible(quest));
    }

    [Fact]
    public void MissingOwnerPartyDoesNotSubstituteAnotherParty()
    {
        var giver = ObjectHelper.SkipConstructor<Hero>();
        var owner = ObjectHelper.SkipConstructor<Hero>();
        var ownership = new IssueOwnershipRegistry();
        ownership.SetOwner(giver, "player-A");
        var player = new Player("player-A", "hero-A", "party-A", "clan-A", "character-A");
        var players = new Mock<IPlayerManager>();
        players.Setup(value => value.TryGetPlayer("player-A", out player)).Returns(true);
        var objects = new Mock<IObjectManager>();
        objects.Setup(value => value.TryGetObjectWithLogging("hero-A", out owner)).Returns(true);
        MobileParty missingParty = null;
        objects.Setup(value => value.TryGetObjectWithLogging("party-A", out missingParty)).Returns(false);
        var service = new ConquestQuest(ownership, players.Object, objects.Object,
            Mock.Of<IIssueGenerationRegistry>(), Mock.Of<IControllerIdProvider>());

        Assert.False(service.TryOpenOwnerScope(giver, out var scope));
        Assert.Null(scope);
    }
}
