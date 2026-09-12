using Common.PacketHandlers;
using Common.Serialization;
using E2E.Tests.Environment;
using GameInterface.Services.Armies.Messages;
using TaleWorlds.CampaignSystem;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Armies;

public class NetworkArmyFullyCreatedSecurityTests : IDisposable
{
    E2ETestEnvironment TestEnvironment { get; }

    public NetworkArmyFullyCreatedSecurityTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    [Fact]
    public void ForgedClientNetworkArmyFullyCreated_IsDroppedBeforeBrokerDispatch()
    {
        // Arrange
        var server = TestEnvironment.Server;
        var forgingClient = TestEnvironment.Clients.First();

        var armyId = "MyArmy";
        server.CreateRegisteredObject<Army>(armyId);

        var serializer = server.Resolve<ICommonSerializer>();
        var forgedPacket = MessagePacket.Create(new NetworkArmyFullyCreated(armyId), serializer);

        // Act
        // A modified client sends the server-only command directly, as if it were a legitimate
        // inbound packet from a connected peer, using a real (already-registered) army id.
        server.SimulatePacket(forgingClient.NetPeer, forgedPacket);

        // Assert
        // MessagePacketHandler.PublishEvent must reject on the IServerToClientCommand check before
        // the message ever reaches the broker, so ArmyHandler.HandleNetworkArmyFullyCreated (and
        // CampaignEventDispatcher.OnArmyCreated) never runs, and nothing gets rebroadcast to clients.
        Assert.Equal(0, server.InternalMessages.GetMessageCount<NetworkArmyFullyCreated>());
        Assert.Equal(0, server.NetworkSentMessages.GetMessageCount<NetworkArmyFullyCreated>());
    }
}