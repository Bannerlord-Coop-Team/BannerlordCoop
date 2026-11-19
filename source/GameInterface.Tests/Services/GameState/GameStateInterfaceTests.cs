using Common;
using Common.Messaging;
using GameInterface.Services.GameState.Interfaces;
using GameInterface.Services.GameState.Messages;
using GameInterface.Services.Modules;
using GameInterface.Services.UI.Interfaces;
using GameInterface.Tests.Utils;
using Moq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.SaveSystem;
using Xunit;

namespace GameInterface.Tests.Services.GameState;

/// <summary>One test blocks the shared game thread pump, so the class runs in the serial game thread collection.</summary>
[Collection(nameof(GameThreadCancellationCollection))]
public class GameStateInterfaceTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

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
            System.Array.Empty<SaveGameFileInfo>,
            _ => { },
            loadTransferredSave: _ => { },
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

    // The first map frame after character creation on a big map can keep the game thread busy past every blocking wait
    [Fact]
    public void LoadSaveData_GameThreadBusyPastTheBlockingTimeout_LoadsAfterLeaving()
    {
        var order = new ConcurrentQueue<string>();
        using var loaded = new ManualResetEventSlim(false);
        var gameStateInterface = new GameStateInterface(Mock.Of<IMessageBroker>(), () => order.Enqueue("EndGame"),
            System.Array.Empty<SaveGameFileInfo>, _ => { }, loadTransferredSave: _ =>
            {
                order.Enqueue("Load");
                loaded.Set();
            }, Mock.Of<INavalSaveCompatibility>(), Mock.Of<ILoadingInterface>());
        using var entered = new ManualResetEventSlim(false);
        using var release = new ManualResetEventSlim(false);

        GameThread.Run(() => { entered.Set(); release.Wait(); });
        try
        {
            Assert.True(entered.Wait(Timeout));
            using (GameThread.Instance.LimitFrameDrain(TimeSpan.FromSeconds(1), TimeSpan.FromMilliseconds(100)))
            {
                // EndGame times out and stays queued, then the load queues behind it
                gameStateInterface.EndGame();
                gameStateInterface.LoadSaveData(new byte[16]);
            }
        }
        finally
        {
            release.Set();
        }

        Assert.True(loaded.Wait(Timeout));
        Assert.Equal(new[] { "EndGame", "Load" }, order);
    }

    // The forced load skips vanilla's NavalDLC check, so a mismatched save must stop before it and leave the loading screen.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LoadGame_NavalMismatch_RefusesBeforeTheForcedLoad(bool orStartNew)
    {
        var saveCheck = new Mock<INavalSaveCompatibility>();
        saveCheck.Setup(check => check.GetHostRefusal("vanilla", new[] { "Native", "Sandbox" })).Returns("refused");
        var loading = new Mock<ILoadingInterface>();
        var forcedLoads = new List<SaveGameFileInfo>();
        var gameStateInterface = new GameStateInterface(Mock.Of<IMessageBroker>(), () => { },
            () => new[] { Save("vanilla", "Native;Sandbox") }, forcedLoads.Add, _ => { }, saveCheck.Object, loading.Object);

        Load(gameStateInterface, "vanilla", orStartNew);

        Assert.Empty(forcedLoads);
        loading.Verify(value => value.HideLoadingScreen(), Times.Once);
    }

    // A save the check allows goes on to the forced load as before.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LoadGame_MatchingSave_ForcesTheLoad(bool orStartNew)
    {
        var loading = new Mock<ILoadingInterface>();
        var forcedLoads = new List<SaveGameFileInfo>();
        var save = Save("naval", "Native;Sandbox;NavalDLC");
        var gameStateInterface = new GameStateInterface(Mock.Of<IMessageBroker>(), () => { },
            () => new[] { save }, forcedLoads.Add, _ => { }, Mock.Of<INavalSaveCompatibility>(), loading.Object);

        Load(gameStateInterface, "naval", orStartNew);

        Assert.Same(save, Assert.Single(forcedLoads));
        loading.Verify(value => value.HideLoadingScreen(), Times.Never);
    }

    // Both host entry points share the save check.
    private static void Load(GameStateInterface gameStateInterface, string saveName, bool orStartNew)
    {
        if (orStartNew) gameStateInterface.LoadGameOrStartNew(saveName);
        else gameStateInterface.LoadGame(saveName);
    }

    // A save listing entry with the given module list.
    private static SaveGameFileInfo Save(string name, string modules)
    {
        var metaData = new MetaData();
        metaData.Add("Modules", modules);
        return new SaveGameFileInfo { Name = name, MetaData = metaData };
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
