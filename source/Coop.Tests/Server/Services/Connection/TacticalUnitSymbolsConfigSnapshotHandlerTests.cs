using Common;
using Common.Messaging;
using Coop.Core.Server.Connections.Messages;
using Coop.Core.Server.Services.Connection.Handlers;
using Coop.Tests.Mocks;
using GameInterface.Services.UI.Interfaces;
using Moq;
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
    public void PlayerCampaignEntered_OnServer_SendsTheCurrentSnapshot()
    {
        var wasServer = ModInformation.IsServer;
        ModInformation.IsServer = true;

        try
        {
            var messageBroker = new MessageBroker();
            var configInterface = new Mock<ITacticalUnitSymbolsConfigInterface>();
            var peer = new TestNetwork().CreatePeer();
            using var handler = new TacticalUnitSymbolsConfigSnapshotHandler(messageBroker, configInterface.Object);

            messageBroker.Publish(this, new PlayerCampaignEntered(peer));

            configInterface.Verify(config => config.SendSnapshot(peer), Times.Once);
        }
        finally
        {
            ModInformation.IsServer = wasServer;
        }
    }
}
