using Common;
using Common.Tests.Utils;
using Coop.Core.Client.Services.TroopRosters.Messages;
using Coop.Core.Server.Services.TroopRosters.Handlers;
using Coop.Tests.Mocks;
using GameInterface.Services.Entity;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Services.TroopRosters.Interfaces;
using GameInterface.Services.TroopRosters.Messages;
using LiteNetLib;
using Moq;
using Serilog;
using System;
using Xunit;

namespace Coop.Tests.Server.Services.TroopRosters;

public class ServerTroopRosterHandlerTests : IDisposable
{
    private const string OwnPartyId = "recruit_own_party";
    private const string OtherPartyId = "recruit_other_party";

    private readonly TestMessageBroker broker = new();
    private readonly TestNetwork network = new();
    private readonly Mock<ILogger> logger = new();
    private readonly Mock<ITroopRosterInterface> troopRosters = new();
    private readonly TroopInfo[] troops = { new TroopInfo("recruiter", "imperial_recruit", 0) };
    private readonly NetPeer peer;
    private readonly ServerTroopRosterHandler handler;

    public ServerTroopRosterHandlerTests()
    {
        var objectManager = Mock.Of<IObjectManager>();
        var players = new PlayerManager(logger.Object, objectManager, new ControllerIdProvider());
        Assert.True(players.AddPlayer(new Player("requester", "hero", OwnPartyId, "clan", "character")));
        peer = network.CreatePeer();
        players.SetPeer("requester", peer);

        handler = new ServerTroopRosterHandler(broker, network, objectManager, players, troopRosters.Object);
    }

    [Fact]
    public void RecruitmentForOwnParty_IsApplied()
    {
        Send(peer, OwnPartyId);

        troopRosters.Verify(service => service.HandleOnRecruitmentDone(OwnPartyId, troops), Times.Once());
        VerifyWarned(Times.Never());
    }

    [Fact]
    public void RecruitmentForAnotherPlayersParty_IsSkippedWithAWarning()
    {
        Send(peer, OtherPartyId);

        VerifyNotApplied();
        VerifyWarned(Times.Once());
    }

    [Fact]
    public void RecruitmentFromASenderThatIsNotAPeer_IsSkipped()
    {
        Send(this, OwnPartyId);

        VerifyNotApplied();
        VerifyWarned(Times.Once());
    }

    [Fact]
    public void RecruitmentFromAnUnregisteredPeer_IsSkipped()
    {
        // Test peers share one endpoint, and peers compare by endpoint, so give this one its own address.
        Send(network.CreatePeer("10.0.0.8"), OwnPartyId);

        VerifyNotApplied();
        VerifyWarned(Times.Once());
    }

    private void Send(object sender, string partyId)
    {
        broker.Publish(sender, new ClientRequestRecruitment(partyId, troops));
        GameThread.Run(() => { }, blocking: true);
    }

    private void VerifyNotApplied() =>
        troopRosters.Verify(service => service.HandleOnRecruitmentDone(It.IsAny<string>(), It.IsAny<TroopInfo[]>()), Times.Never());

    private void VerifyWarned(Times times) =>
        logger.Verify(log => log.Warning(
            "Ignoring a request for party {PartyId} from peer {PeerId}, which does not control it",
            It.IsAny<string>(), It.IsAny<int?>()), times);

    public void Dispose() => handler.Dispose();
}
