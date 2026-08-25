#if DEBUG
using Common.Messaging;
using Common.Network;
using GameInterface.Services.MapEvents.Commands;
using GameInterface.Services.Missions;
using GameInterface.Services.Players;
using LiteNetLib;
using Moq;
using System.Collections.Generic;
using Xunit;

namespace GameInterface.Tests.Services.MapEvents;

public class DebugBattleMissionExitRequesterTests
{
    [Fact]
    public void Request_SendsOnceToEachConnectedMissionMember()
    {
        var network = new Mock<INetwork>();
        var playerManager = new Mock<IPlayerManager>();
        var missionMembership = new Mock<IMissionMembershipRegistry>();
        NetPeer peer = null!;
        missionMembership.Setup(registry => registry.IsControllerInMission("first")).Returns(true);
        missionMembership.Setup(registry => registry.IsControllerInMission("second")).Returns(true);
        playerManager.Setup(manager => manager.TryGetPeer("first", out peer)).Returns(true);
        playerManager.Setup(manager => manager.TryGetPeer("second", out peer)).Returns(true);
        var requester = new DebugBattleMissionExitRequester(
            network.Object,
            playerManager.Object,
            missionMembership.Object);

        int requested = requester.Request(
            "MapEvent_Created_792",
            new[] { "first", "second", "first" });

        Assert.Equal(2, requested);
        network.Verify(
            instance => instance.Send(
                peer,
                It.Is<NetworkEndDebugBattleMission>(message =>
                    message.MapEventId == "MapEvent_Created_792")),
            Times.Exactly(2));
    }

    [Fact]
    public void Request_SkipsControllersOutsideTheMissionOrWithoutAPeer()
    {
        var network = new Mock<INetwork>();
        var playerManager = new Mock<IPlayerManager>();
        var missionMembership = new Mock<IMissionMembershipRegistry>();
        NetPeer peer = null!;
        missionMembership.Setup(registry => registry.IsControllerInMission("not-in-mission")).Returns(false);
        missionMembership.Setup(registry => registry.IsControllerInMission("disconnected")).Returns(true);
        playerManager.Setup(manager => manager.TryGetPeer("disconnected", out peer)).Returns(false);
        var requester = new DebugBattleMissionExitRequester(
            network.Object,
            playerManager.Object,
            missionMembership.Object);

        int requested = requester.Request(
            "MapEvent_Created_792",
            new List<string> { "not-in-mission", "disconnected" });

        Assert.Equal(0, requested);
        network.Verify(
            instance => instance.Send(It.IsAny<NetPeer>(), It.IsAny<IMessage>()),
            Times.Never);
    }
}
#endif
