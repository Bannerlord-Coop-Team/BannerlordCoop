using Common;
using Common.Messaging;
using GameInterface.Services.GameState.Interfaces;
using GameInterface.Services.GameState.Messages;
using GameInterface.Services.Modules;
using GameInterface.Services.UI.Interfaces;
using GameInterface.Tests.Utils;
using Moq;
using System;
using System.Runtime.CompilerServices;
using System.Threading;
using TaleWorlds.SaveSystem;
using Xunit;

namespace GameInterface.Tests.Services.GameState;

/// <summary>Blocks the shared game thread pump, so it runs in the serial game thread collection.</summary>
[Collection(nameof(GameThreadCancellationCollection))]
public sealed class GameStateInterfaceSaveLoadTests : IDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    // A short queue timeout, so each test's start timeout is the wait that counts
    private readonly IDisposable shortQueueTimeout =
        GameThread.Instance.LimitFrameDrain(TimeSpan.FromSeconds(1), TimeSpan.FromMilliseconds(100));

    static GameStateInterfaceSaveLoadTests()
    {
        RuntimeHelpers.RunModuleConstructor(typeof(Coop.Tests.Mocks.TestNetwork).Module.ModuleHandle);
    }

    public void Dispose() => shortQueueTimeout.Dispose();

    [Fact]
    public void TryLoadSaveData_LoadNeverStarted_ReturnsFalse()
    {
        var messageBroker = new Mock<IMessageBroker>();
        bool loadRan = false;
        var gameStateInterface = new GameStateInterface(messageBroker.Object, () => { }, Array.Empty<SaveGameFileInfo>,
            _ => { }, loadTransferredSave: _ => loadRan = true,
            navalSaveCompatibility: Mock.Of<INavalSaveCompatibility>(), loadingInterface: Mock.Of<ILoadingInterface>());
        using var entered = new ManualResetEventSlim(false);
        using var release = new ManualResetEventSlim(false);
        bool loadStarted;

        // A busy game thread, like the first map frame after character creation on a big map
        GameThread.Run(() => { entered.Set(); release.Wait(); });
        try
        {
            Assert.True(entered.Wait(Timeout));
            loadStarted = gameStateInterface.TryLoadSaveData(new byte[16], TimeSpan.FromMilliseconds(200));
        }
        finally
        {
            release.Set();
        }

        // Drain the queue so an expired load would have run by now
        GameThread.Run(() => { }, blocking: true);
        // The caller ends the join only for a load that never started
        Assert.False(loadStarted);
        Assert.False(loadRan);
        messageBroker.Verify(broker => broker.Publish(It.IsAny<object>(), It.IsAny<GameLoadStarted>()), Times.Once);
    }

    [Fact]
    public void TryLoadSaveData_StartedLoadOutlastsTheWait_KeepsLoading()
    {
        using var loadStarted = new ManualResetEventSlim(false);
        using var release = new ManualResetEventSlim(false);
        var gameStateInterface = new GameStateInterface(Mock.Of<IMessageBroker>(), () => { }, Array.Empty<SaveGameFileInfo>,
            _ => { }, loadTransferredSave: _ =>
            {
                loadStarted.Set();
                release.Wait(Timeout);
            }, navalSaveCompatibility: Mock.Of<INavalSaveCompatibility>(), loadingInterface: Mock.Of<ILoadingInterface>());

        try
        {
            // The load starts and then runs past both short waits, like reading a big save. The wait leaves
            // a slow CI runner time to start the load first.
            Assert.True(gameStateInterface.TryLoadSaveData(new byte[16], TimeSpan.FromSeconds(1)));
            Assert.True(loadStarted.IsSet);
        }
        finally
        {
            release.Set();
        }

        GameThread.Run(() => { }, blocking: true);
    }

    [Fact]
    public void TryLoadSaveData_WaitsTheStartTimeout()
    {
        bool loadRan = false;
        var gameStateInterface = new GameStateInterface(Mock.Of<IMessageBroker>(), () => { }, Array.Empty<SaveGameFileInfo>,
            _ => { }, loadTransferredSave: _ => loadRan = true,
            navalSaveCompatibility: Mock.Of<INavalSaveCompatibility>(), loadingInterface: Mock.Of<ILoadingInterface>());
        using var entered = new ManualResetEventSlim(false);

        // Busy past the queue timeout but not past the start timeout
        GameThread.Run(() => { entered.Set(); Thread.Sleep(500); });
        Assert.True(entered.Wait(Timeout));

        Assert.True(gameStateInterface.TryLoadSaveData(new byte[16], Timeout));
        Assert.True(loadRan);
    }
}
