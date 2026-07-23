using Common.Messaging;
using Common.Network;
using Common.Network.Messages;
using Common.Network.Session;
using Common.PacketHandlers;
using Common.Serialization;
using Coop.IntegrationTests.Environment;
using Coop.IntegrationTests.Environment.Instance;
using Coop.IntegrationTests.Kingdoms;
using GameInterface.Services.Entity;
using LiteNetLib;
using Missions.Messages;
using Missions.Services.Network;
using Moq;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;

namespace Coop.IntegrationTests.Missions;

/// <summary>
/// BR-030 (Disconnected Player Detection): "A player shall be considered disconnected from an active battle
/// mission when the player's connection to the campaign server is unexpectedly lost. The campaign server shall
/// detect the disconnection and is responsible for notifying the battle mission's remaining participants
/// (including the mission host). Loss of peer-to-peer connectivity within the mission mesh alone, while the
/// player's server connection remains, does not constitute disconnection from the mission."
///
/// Positive: a server-observed <see cref="PlayerDisconnected"/> (the campaign-server connection dropping) fans a
/// <see cref="MissionPeerDisconnected"/> to every remaining member's connection and raises exactly one
/// <see cref="MissionMemberDeparted"/> marked as an ungraceful drop so the reserve remains available to the
/// battle host. Negative: a mesh-only peer
/// drop (<see cref="LiteNetP2PClient.OnPeerDisconnected"/>) — the P2P link dying while the server link is intact
/// — must NOT fabricate either mission-disconnect notification.
/// </summary>
[Collection(KingdomSyncGameThreadCollection.Name)]
public class MissionDisconnectDetectionTests
{
    private const string InstanceId = "battle-instance";

    // 1 server + 3 clients so a disconnect leaves more than one remaining member to notify.
    internal TestEnvironment TestEnvironment { get; } = new TestEnvironment(numClients: 3);

    [Fact]
    [Trait("Requirement", "BR-030")]
    public void ServerDetectedDisconnect_NotifiesRemainingMembers_AndMarksTheDeparture()
    {
        var members = SetupClients();

        // Capture the MissionPeerDisconnected each member's connection receives.
        var received = members.ToDictionary(m => m.ControllerId, _ => new List<MissionPeerDisconnected>());
        foreach (var member in members)
        {
            var id = member.ControllerId;
            member.Instance.Subscribe<MissionPeerDisconnected>(p => received[id].Add(p.What));
        }

        // Capture the server's local host-migration/cleanup signal.
        var departures = new List<MissionMemberDeparted>();
        TestEnvironment.Server.Subscribe<MissionMemberDeparted>(p => departures.Add(p.What));

        foreach (var member in members)
            Join(member);

        // The campaign-server connection for the third member is lost.
        Disconnect(members[2]);

        // Both remaining members were told the third dropped; the dropped member itself receives nothing.
        var dropped = members[2].ControllerId;
        Assert.Equal(dropped, Assert.Single(received[members[0].ControllerId]).ControllerId);
        Assert.Equal(InstanceId, received[members[0].ControllerId][0].InstanceId);
        Assert.Equal(dropped, Assert.Single(received[members[1].ControllerId]).ControllerId);
        Assert.Equal(InstanceId, received[members[1].ControllerId][0].InstanceId);
        Assert.Empty(received[dropped]);

        // Exactly one ungraceful departure, so the player's remaining reserve can fall to the battle host,
        // and the instance still has members.
        var departure = Assert.Single(departures);
        Assert.Equal(dropped, departure.ControllerId);
        Assert.Equal(InstanceId, departure.InstanceId);
        Assert.False(departure.WasRetreat);
        Assert.False(departure.IsInstanceEmpty);
    }

    [Fact]
    [Trait("Requirement", "BR-030")]
    public void LastMemberDisconnect_MarksTheInstanceEmpty()
    {
        var members = SetupClients().Take(2).ToArray();

        var departures = new List<MissionMemberDeparted>();
        TestEnvironment.Server.Subscribe<MissionMemberDeparted>(p => departures.Add(p.What));

        Join(members[0]);
        Join(members[1]);

        Disconnect(members[1]);
        var first = Assert.Single(departures);
        Assert.Equal(members[1].ControllerId, first.ControllerId);
        Assert.False(first.WasRetreat);
        Assert.False(first.IsInstanceEmpty);

        departures.Clear();
        Disconnect(members[0]);
        var last = Assert.Single(departures);
        Assert.Equal(members[0].ControllerId, last.ControllerId);
        Assert.False(last.WasRetreat);
        Assert.True(last.IsInstanceEmpty);
    }

    [Fact]
    [Trait("Requirement", "BR-030")]
    public void DisconnectOfAPeerNotInTheInstance_NotifiesNoOne()
    {
        var members = SetupClients();

        var received = members.ToDictionary(m => m.ControllerId, _ => new List<MissionPeerDisconnected>());
        foreach (var member in members)
        {
            var id = member.ControllerId;
            member.Instance.Subscribe<MissionPeerDisconnected>(p => received[id].Add(p.What));
        }

        var departures = new List<MissionMemberDeparted>();
        TestEnvironment.Server.Subscribe<MissionMemberDeparted>(p => departures.Add(p.What));

        // Only two of the three ever entered the mission instance.
        Join(members[0]);
        Join(members[1]);

        // The third client's server connection drops, but it was never in this (or any) mission instance, so the
        // server has nothing to notify about — the detection only fires for actual mission members.
        Disconnect(members[2]);

        Assert.Empty(departures);
        foreach (var member in members)
            Assert.Empty(received[member.ControllerId]);
    }

    [Fact]
    [Trait("Requirement", "BR-030")]
    public void MeshPeerDrop_DoesNotNotifyTheMissionOfADisconnection()
    {
        // A mesh-only P2P drop is handled by the client, not the server, and must not raise the server-authored
        // mission-disconnect notifications: BR-030 says P2P loss alone (server link intact) is not a mission
        // disconnection. Build the client with mocked collaborators and drive the mesh-drop callback directly.
        var config = new Mock<INetworkConfig>();
        var relayNetwork = new Mock<IRelayNetwork>();
        var missionContext = new Mock<IMissionContext>();
        var serializer = new Mock<ICommonSerializer>();
        var messageBroker = new Mock<IMessageBroker>();
        var packetManager = new Mock<IPacketManager>();
        var controllerIdProvider = new Mock<IControllerIdProvider>();
        var steamBridge = new Mock<ISteamMissionBridge>();

        var client = new LiteNetP2PClient(
            config.Object,
            relayNetwork.Object,
            missionContext.Object,
            serializer.Object,
            messageBroker.Object,
            packetManager.Object,
            controllerIdProvider.Object,
            steamBridge.Object);

        var droppedPeer = CreatePeer(new IPEndPoint(IPAddress.Loopback, 55001), 1);

        client.OnPeerDisconnected(droppedPeer, default);

        // Neither the server→members disconnect notice nor the local departure signal is fabricated by a mesh drop.
        messageBroker.Verify(b => b.Publish(It.IsAny<object>(), It.IsAny<MissionPeerDisconnected>()), Times.Never);
        messageBroker.Verify(b => b.Publish(It.IsAny<object>(), It.IsAny<MissionMemberDeparted>()), Times.Never);
    }

    private record Member(EnvironmentInstance Instance, string ControllerId);

    private List<Member> SetupClients()
    {
        var members = new List<Member>();
        int i = 0;
        foreach (var client in TestEnvironment.Clients)
        {
            var controllerId = $"Client{++i}";
            client.Resolve<IControllerIdProvider>().SetControllerId(controllerId);
            members.Add(new Member(client, controllerId));
        }
        return members;
    }

    /// <summary>Simulates the server receiving a MissionEntered over the member's connection.</summary>
    private void Join(Member member) =>
        GameThreadTestRunner.Run(() =>
            TestEnvironment.Server.SimulateMessage(
                member.Instance.NetPeer,
                new NetworkMissionEntered(member.ControllerId, InstanceId)));

    /// <summary>
    /// Simulates the campaign server observing the member's connection drop ungracefully. Handle_PlayerDisconnected
    /// resolves the instance from the bare peer via <c>MissionManager.TryHandleDisconnect</c>.
    /// </summary>
    private void Disconnect(Member member) =>
        GameThreadTestRunner.Run(() =>
            TestEnvironment.Server.SimulateMessage(
                this,
                new PlayerDisconnected(member.Instance.NetPeer, default)));

    private static readonly ConstructorInfo PeerConstructor = typeof(NetPeer).GetConstructor(
        BindingFlags.NonPublic | BindingFlags.Instance,
        binder: null,
        new[] { typeof(NetManager), typeof(IPEndPoint), typeof(int) },
        modifiers: null)!;

    private static NetPeer CreatePeer(IPEndPoint endpoint, int id)
        => (NetPeer)PeerConstructor.Invoke(new object[] { new NetManager(null), endpoint, id });
}
