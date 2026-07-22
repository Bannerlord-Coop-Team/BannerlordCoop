using Common;
using Common.Messaging;
using Coop.Core.Server.Connections.Messages;
using Coop.Core.Server.Services.Connection.Handlers;
using Coop.Tests;
using Coop.Tests.Mocks;
using GameInterface.Services.UI.Interfaces;
using Moq;
using System;
using System.Runtime.CompilerServices;
using System.Threading;
using Xunit;

namespace Coop.Tests.Server.Services.Connection;

[Collection(ModInformationRoleCollection.Name)]
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
            using var snapshotSent = new ManualResetEventSlim(false);
            GameThread.Run(() => gameThreadId = Environment.CurrentManagedThreadId, blocking: true);
            configInterface.Setup(config => config.SendSnapshot(peer))
                .Callback(() =>
                {
                    snapshotThreadId = Environment.CurrentManagedThreadId;
                    snapshotSent.Set();
                });
            using var handler = new TacticalUnitSymbolsConfigSnapshotHandler(messageBroker, configInterface.Object);

            messageBroker.Publish(this, new PlayerCampaignEntered(peer));

            Assert.True(snapshotSent.Wait(TimeSpan.FromSeconds(5)), "snapshot was not sent within the timeout");
            configInterface.Verify(config => config.SendSnapshot(peer), Times.Once);
            Assert.Equal(gameThreadId, snapshotThreadId);
        }
        finally
        {
            ModInformation.IsServer = wasServer;
        }
    }
}
