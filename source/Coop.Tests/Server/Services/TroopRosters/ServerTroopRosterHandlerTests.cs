using Common;
using Common.Tests.Utils;
using Coop.Core.Client.Services.TroopRosters.Messages;
using Coop.Core.Server.Services.TroopRosters.Handlers;
using Coop.Tests.Mocks;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.TroopRosters.Interfaces;
using GameInterface.Services.TroopRosters.Messages;
using LiteNetLib;
using Moq;
using System;
using Xunit;

namespace Coop.Tests.Server.Services.TroopRosters;

public class ServerTroopRosterHandlerTests : IDisposable
{
    private readonly TestMessageBroker broker = new();
    private readonly TestNetwork network = new();
    private readonly Mock<ITroopRosterInterface> troopRosterInterface = new();
    private readonly NetPeer peer;
    private readonly ServerTroopRosterHandler handler;

    public ServerTroopRosterHandlerTests()
    {
        peer = network.CreatePeer();
        handler = new ServerTroopRosterHandler(broker, network, new Mock<IObjectManager>().Object, troopRosterInterface.Object);
    }

    [Fact]
    public void SkippedCartEntries_TellTheRecruitingClient()
    {
        troopRosterInterface.Setup(i => i.HandleOnRecruitmentDone("party", It.IsAny<TroopInfo[]>())).Returns(2);

        Send();

        var message = Assert.Single(network.GetPeerMessagesFromType<SendInformationMessage>(peer));
        Assert.StartsWith("2 troop(s) could not be recruited", message.Text);
    }

    [Fact]
    public void FullyRecruitedCart_SendsNoMessage()
    {
        troopRosterInterface.Setup(i => i.HandleOnRecruitmentDone("party", It.IsAny<TroopInfo[]>())).Returns(0);

        Send();

        troopRosterInterface.Verify(i => i.HandleOnRecruitmentDone("party", It.IsAny<TroopInfo[]>()), Times.Once);
        Assert.False(network.SentNetworkMessages.ContainsKey(peer.Id));
    }

    private void Send()
    {
        broker.Publish(peer, new ClientRequestRecruitment("party", new[] { new TroopInfo("hero", "troop", 0) }));
        GameThread.Run(() => { }, blocking: true);
    }

    public void Dispose() => handler.Dispose();
}
