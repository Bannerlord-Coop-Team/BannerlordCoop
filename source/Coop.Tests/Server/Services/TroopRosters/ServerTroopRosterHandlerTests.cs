using Common;
using Common.Logging;
using Common.Tests.Utils;
using Coop.Core.Client.Services.TroopRosters.Messages;
using Coop.Core.Server.Services.TroopRosters.Handlers;
using Coop.Tests.Mocks;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Services.TroopRosters.Interfaces;
using GameInterface.Services.TroopRosters.Messages;
using LiteNetLib;
using Moq;
using System;
using System.Collections.Concurrent;
using Xunit;

namespace Coop.Tests.Server.Services.TroopRosters;

public class ServerTroopRosterHandlerTests : IDisposable
{
    private const string OwnPartyId = "recruit_own_party";
    private const string OtherPartyId = "recruit_other_party";

    private readonly TestMessageBroker broker = new();
    private readonly TestNetwork network = new();
    private readonly Mock<IPlayerManager> players = new();
    private readonly Mock<ITroopRosterInterface> troopRosters = new();
    private readonly ConcurrentQueue<string> logs = new();
    private readonly Action<string> captureLog;
    private readonly TroopInfo[] troops = { new TroopInfo("recruiter", "imperial_recruit", 0) };
    private readonly NetPeer peer;
    private readonly ServerTroopRosterHandler handler;

    public ServerTroopRosterHandlerTests()
    {
        captureLog = logs.Enqueue;
        OutputSinkManager.AddLogCallback(captureLog);

        peer = network.CreatePeer();
        var player = new Player("requester", "hero", OwnPartyId, "clan", "character");
        players.Setup(manager => manager.TryGetPlayer(peer, out player)).Returns(true);

        handler = new ServerTroopRosterHandler(broker, network, Mock.Of<IObjectManager>(), players.Object, troopRosters.Object);
    }

    [Fact]
    public void RecruitmentForOwnParty_IsApplied()
    {
        Send(OwnPartyId);

        troopRosters.Verify(service => service.HandleOnRecruitmentDone(OwnPartyId, troops), Times.Once());
    }

    [Fact]
    public void RecruitmentForAnotherPlayersParty_IsSkippedWithAWarning()
    {
        Send(OtherPartyId);

        troopRosters.Verify(service => service.HandleOnRecruitmentDone(It.IsAny<string>(), It.IsAny<TroopInfo[]>()), Times.Never());
        Assert.Single(logs, line => line.Contains("Ignoring recruitment for party") && line.Contains(OtherPartyId));
    }

    private void Send(string partyId)
    {
        broker.Publish(peer, new ClientRequestRecruitment(partyId, troops));
        GameThread.Run(() => { }, blocking: true);
    }

    public void Dispose()
    {
        handler.Dispose();
        OutputSinkManager.RemoveLogCallback(captureLog);
    }
}
