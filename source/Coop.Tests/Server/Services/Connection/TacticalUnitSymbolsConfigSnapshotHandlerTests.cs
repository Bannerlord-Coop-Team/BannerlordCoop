using Common;
using Common.Messaging;
using Coop.Core.Server.Connections.Messages;
using Coop.Core.Server.Services.Connection.Handlers;
using Coop.Tests.Mocks;
using GameInterface.Services.UI.Interfaces;
using Moq;
using System;
using System.Runtime.CompilerServices;
using Xunit;

namespace Coop.Tests.Server.Services.Connection;

public class TacticalUnitSymbolsConfigSnapshotHandlerTests
{
    static TacticalUnitSymbolsConfigSnapshotHandlerTests()
    {
        RuntimeHelpers.RunModuleConstructor(typeof(TestNetwork).Module.ModuleHandle);
    }

    [Fact]
    public void PlayerCampaignEntered_OnServer_SendsTheCurrentSnapshotOnTheGameThread()
    {
        var wasServer = ModInformation.IsServer;
        ModInformation.IsServer = true;

        try
        {
            var messageBroker = new MessageBroker();
            var configInterface = new Mock<ITacticalUnitSymbolsConfigInterface>();
            var peer = new TestNetwork().CreatePeer();
            int gameThreadId = 0;
            int snapshotThreadId = 0;
            GameThread.Run(() => gameThreadId = Environment.CurrentManagedThreadId, blocking: true);
            configInterface.Setup(config => config.SendSnapshot(peer))
                .Callback(() => snapshotThreadId = Environment.CurrentManagedThreadId);
            using var handler = new TacticalUnitSymbolsConfigSnapshotHandler(messageBroker, configInterface.Object);

            messageBroker.Publish(this, new PlayerCampaignEntered(peer));

            configInterface.Verify(config => config.SendSnapshot(peer), Times.Once);
            Assert.Equal(gameThreadId, snapshotThreadId);
        }
        finally
        {
            ModInformation.IsServer = wasServer;
        }
    }
}
