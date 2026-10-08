using Common;
using Common.Tests.Utils;
using Common.Util;
using Coop.Core.Server.Services.Issues;
using Coop.Core.Server.Services.Save.Messages;
using GameInterface.Services.Heroes.HeirSelection.Messages;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using Moq;
using TaleWorlds.CampaignSystem;
using Xunit;

namespace Coop.Tests.Server.Services.Issues;

public sealed class ArtisanProductLifecycleHandlerTests
{
    [Fact]
    public void RestoredRegistrationsAndHeirSelectionReachOnlyTheRegisteredOwnerOnGameThread()
    {
        var hero = ObjectHelper.SkipConstructor<Hero>();
        var objects = new Mock<IObjectManager>();
        var heroId = "second-heir";
        objects.Setup(manager => manager.TryGetIdWithLogging(hero, out heroId)).Returns(true);
        var players = new Mock<IPlayerManager>();
        players.SetupGet(manager => manager.Players).Returns(new[]
        {
            new Player("first-owner", "first-hero", "first-party", "first-clan", "first-character"),
            new Player("second-owner", heroId, "second-party", "second-clan", "second-character"),
        });
        var actions = new Mock<IArtisanProductQuestActions>(MockBehavior.Strict);
        actions.Setup(action => action.CancelInvalidLoadedIssues()).Callback(() => Assert.True(GameThread.Instance.IsGameThread));
        actions.Setup(action => action.CancelForPlayer("second-owner")).Callback(() => Assert.True(GameThread.Instance.IsGameThread));
        var broker = new TestMessageBroker();
        using var handler = new ArtisanProductLifecycleHandler(broker, objects.Object, players.Object, actions.Object);
        broker.Publish(this, new SavedPlayerRegistrationsRestored());
        broker.Publish(this, new PlayerHeirSelectionCompleted(hero));
        broker.Publish(this, new PlayerHeirSelectionCompleted(ObjectHelper.SkipConstructor<Hero>()));
        GameThread.Run(() => { }, blocking: true);
        actions.Verify(action => action.CancelInvalidLoadedIssues(), Times.Once);
        actions.Verify(action => action.CancelForPlayer("second-owner"), Times.Once);
        actions.VerifyNoOtherCalls();
    }
}
