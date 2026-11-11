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
public sealed class GameStateInterfaceSaveLoadTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    static GameStateInterfaceSaveLoadTests()
    {
        RuntimeHelpers.RunModuleConstructor(typeof(Coop.Tests.Mocks.TestNetwork).Module.ModuleHandle);
    }

    [Fact]
    public void LoadSaveData_LoadNeverStarted_ThrowsTheTimeout()
    {
        var messageBroker = new Mock<IMessageBroker>();
        bool loadRan = false;
        var gameStateInterface = new GameStateInterface(messageBroker.Object, () => { }, Array.Empty<SaveGameFileInfo>,
            _ => { }, Mock.Of<INavalSaveCompatibility>(), Mock.Of<ILoadingInterface>(), _ => loadRan = true);
        using var entered = new ManualResetEventSlim(false);
        using var release = new ManualResetEventSlim(false);
        TimeoutException timeout;

        // A busy game thread, like the first map frame after character creation on a big map
        GameThread.Run(() => { entered.Set(); release.Wait(); });
        try
        {
            Assert.True(entered.Wait(Timeout));
            using (GameThread.Instance.UseBlockingTimeout(TimeSpan.FromMilliseconds(200)))
            {
                timeout = Assert.Throws<TimeoutException>(() => gameStateInterface.LoadSaveData(new byte[16]));
            }
        }
        finally
        {
            release.Set();
        }

        // The caller ends the join only for a load that never started
        GameThread.Run(() => { }, blocking: true);
        Assert.False(loadRan);
        Assert.Contains("will not run", timeout.Message);
        messageBroker.Verify(broker => broker.Publish(It.IsAny<object>(), It.IsAny<GameLoadStarted>()), Times.Once);
    }

    [Fact]
    public void LoadSaveData_StartedLoadOutlastsTheWait_KeepsLoading()
    {
        using var loadStarted = new ManualResetEventSlim(false);
        using var release = new ManualResetEventSlim(false);
        var gameStateInterface = new GameStateInterface(Mock.Of<IMessageBroker>(), () => { }, Array.Empty<SaveGameFileInfo>,
            _ => { }, Mock.Of<INavalSaveCompatibility>(), Mock.Of<ILoadingInterface>(), _ =>
            {
                loadStarted.Set();
                release.Wait(Timeout);
            });

        try
        {
            // The load starts at once and then runs past both short waits, like reading a big save
            using (GameThread.Instance.UseBlockingTimeout(TimeSpan.FromMilliseconds(200)))
            {
                gameStateInterface.LoadSaveData(new byte[16]);
            }

            Assert.True(loadStarted.IsSet);
        }
        finally
        {
            release.Set();
        }

        GameThread.Run(() => { }, blocking: true);
    }
}
