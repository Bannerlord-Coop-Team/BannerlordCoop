using Common.Messaging;
using GameInterface.Services.GameState.Interfaces;
using GameInterface.Services.GameState.Messages;
using GameInterface.Services.Modules;
using GameInterface.Services.UI.Interfaces;
using Moq;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.SaveSystem;
using Xunit;

namespace GameInterface.Tests.Services.GameState;

public class GameStateInterfaceTests
{
    static GameStateInterfaceTests()
    {
        // EndGame marshals to the game thread; start the Coop.Tests pump even when this class runs alone.
        RuntimeHelpers.RunModuleConstructor(typeof(Coop.Tests.Mocks.TestNetwork).Module.ModuleHandle);
    }

    [Fact]
    public void EndGame_DoesNotPublishMainMenuEnteredBeforeInitialStateActivation()
    {
        var messageBroker = new Mock<IMessageBroker>();
        bool endGameCalled = false;
        var gameStateInterface = new GameStateInterface(
            messageBroker.Object,
            () => endGameCalled = true,
            Mock.Of<INavalSaveCompatibility>(),
            Mock.Of<ILoadingInterface>());

        gameStateInterface.EndGame();

        Assert.True(endGameCalled);
        messageBroker.Verify(
            broker => broker.Publish(
                It.IsAny<object>(),
                It.IsAny<MainMenuEntered>()),
            Times.Never);
    }

    // The forced load skips vanilla's NavalDLC check, so a mismatched save must stop here and leave the loading screen.
    [Fact]
    public void RefuseNavalMismatch_StopsTheLoad_AndHidesTheLoadingScreen()
    {
        var saveCheck = new Mock<INavalSaveCompatibility>();
        saveCheck.Setup(check => check.GetHostRefusal("vanilla", new[] { "Native", "Sandbox" })).Returns("refused");
        var loading = new Mock<ILoadingInterface>();
        var gameStateInterface = new GameStateInterface(Mock.Of<IMessageBroker>(), () => { }, saveCheck.Object, loading.Object);
        var metaData = new MetaData();
        metaData.Add("Modules", "Native;Sandbox");

        bool refused = gameStateInterface.RefuseNavalMismatch(new SaveGameFileInfo { Name = "vanilla", MetaData = metaData });

        Assert.True(refused);
        loading.Verify(value => value.HideLoadingScreen(), Times.Once);
    }

    // A save the check allows loads as before.
    [Fact]
    public void RefuseNavalMismatch_LetsAMatchingSaveLoad()
    {
        var loading = new Mock<ILoadingInterface>();
        var gameStateInterface = new GameStateInterface(Mock.Of<IMessageBroker>(), () => { },
            Mock.Of<INavalSaveCompatibility>(), loading.Object);

        bool refused = gameStateInterface.RefuseNavalMismatch(new SaveGameFileInfo { Name = "naval", MetaData = new MetaData() });

        Assert.False(refused);
        loading.Verify(value => value.HideLoadingScreen(), Times.Never);
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
