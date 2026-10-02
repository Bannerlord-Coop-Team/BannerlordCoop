#if DEBUG
using GameInterface.Services.SiegeEvents.Commands;
using ProtoBuf;
using System.IO;
using Xunit;
using Common;
using Common.Commands;
using Common.Messaging;
using Common.Network;
using Coop.IntegrationTests.Kingdoms;
using GameInterface.Services.MobileParties.Data;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.MapEvents.Commands;
using Moq;

namespace Coop.IntegrationTests.Settlements;

[Collection(KingdomSyncGameThreadCollection.Name)]
public class Issue1837FixtureTests
{
    [Fact]
    public void ClientCannotDispatchServerContinuationOrRout()
    {
        var originalRole = ModInformation.IsServer;
        var objects = new Mock<IObjectManager>(MockBehavior.Strict);
        var players = new Mock<IPlayerManager>(MockBehavior.Strict);
        var network = new Mock<INetwork>(MockBehavior.Strict);
        var broker = new Mock<IMessageBroker>(MockBehavior.Strict);
        var snapshot = new Mock<IMobilePartyBehaviorSnapshot>(MockBehavior.Strict);
        var args = new Mock<ICoopCommandArgs>(MockBehavior.Strict);
        try
        {
            ModInformation.IsServer = false;
            var continuation = new Issue1837ContinuationCoopCommand(objects.Object, players.Object,
                network.Object, broker.Object, snapshot.Object);
            var rout = new Issue1837RouteEnemiesCoopCommand(objects.Object, players.Object, network.Object);
            Assert.False(continuation.ProcessCommand(args.Object).Succeeded);
            Assert.False(rout.ProcessCommand(args.Object).Succeeded);
            network.VerifyNoOtherCalls();
            broker.VerifyNoOtherCalls();
            objects.VerifyNoOtherCalls();
            players.VerifyNoOtherCalls();
        }
        finally { ModInformation.IsServer = originalRole; }
    }

    [Theory]
    [InlineData("enter")]
    [InlineData("assault")]
    public void ProductionContinuationPreservesSelectedPlayerAndAction(string action)
    {
        var original = new NetworkIssue1837Continuation("testclient2", "town_ES1", action);
        using var stream = new MemoryStream();
        Serializer.Serialize(stream, original);
        stream.Position = 0;
        var copy = Serializer.Deserialize<NetworkIssue1837Continuation>(stream);
        Assert.Equal(original.ControllerId, copy.ControllerId);
        Assert.Equal(original.SettlementId, copy.SettlementId);
        Assert.Equal(action, copy.Action);
    }
}
#endif
