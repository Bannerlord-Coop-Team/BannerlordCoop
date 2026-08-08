using Common.Messaging;
using GameInterface.Services.GameState.Interfaces;
using GameInterface.Services.GameState.Messages;
using Moq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using Xunit;

namespace GameInterface.Tests.Services.GameState;

public class GameStateInterfaceTests
{
    [Fact]
    public void EndGame_DoesNotPublishMainMenuEnteredBeforeInitialStateActivation()
    {
        var messageBroker = new Mock<IMessageBroker>();
        bool endGameCalled = false;
        var gameStateInterface = new GameStateInterface(
            messageBroker.Object,
            () => endGameCalled = true);

        gameStateInterface.EndGame();

        Assert.True(endGameCalled);
        messageBroker.Verify(
            broker => broker.Publish(
                It.IsAny<object>(),
                It.IsAny<MainMenuEntered>()),
            Times.Never);
    }

    [Fact]
    public void ClearTransferredMapNotices_DropsHistoryWithoutSuppressingFutureNotices()
    {
        var informationManager = new CampaignInformationManager();
        informationManager._mapNotices.Add(new TestInformationData("Historical"));

        GameStateInterface.ClearTransferredMapNotices(informationManager);
        informationManager.OnGameLoaded();

        Assert.Empty(informationManager._mapNotices);

        var liveNotice = new TestInformationData("Live");
        informationManager.NewMapNoticeAdded(liveNotice);

        Assert.Same(liveNotice, Assert.Single(informationManager._mapNotices));
    }

    private sealed class TestInformationData : InformationData
    {
        public TestInformationData(string description) : base(new TextObject(description))
        {
        }

        public override TextObject TitleText => DescriptionText;

        public override string SoundEventPath => string.Empty;
    }
}
