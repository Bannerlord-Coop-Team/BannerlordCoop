using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Registry.Auto;
using GameInterface.Services.MobileParties.Handlers;
using GameInterface.Services.MobileParties.Messages.Lifetime;
using GameInterface.Services.ObjectManager;
using Moq;
using TaleWorlds.CampaignSystem.Party;
using Xunit;

namespace GameInterface.Tests.Services.MobileParties;

/// <summary>
/// Unit tests for the server send-side of party destruction replication in
/// <see cref="PartyLifetimeHandler"/>. A party destroyed with a null destroyer (e.g. vanilla
/// patrol culling) must still be replicated to clients instead of leaving a zombie party behind.
/// The handler must never publish <see cref="InstanceDestroyed{T}"/> itself: registry teardown is
/// owned by the MobileParty.RemoveParty postfix, which vanilla runs after the destroy completes.
/// </summary>
public class PartyLifetimeHandlerTests
{
    private readonly Mock<IMessageBroker> messageBroker = new();
    private readonly Mock<IObjectManager> objectManager = new();
    private readonly Mock<INetwork> network = new();
    private readonly PartyLifetimeHandler handler;

    private object? sentMessage;

    public PartyLifetimeHandlerTests()
    {
        handler = new PartyLifetimeHandler(messageBroker.Object, objectManager.Object, network.Object);

        network.Setup(n => n.SendAll(It.IsAny<IMessage>()))
            .Callback<IMessage>(message => sentMessage = message);
    }

    [Fact]
    public void Handle_PartyDestroyed_NullVictoriousParty_ReplicatesWithNullVictorId()
    {
        var defeated = ObjectHelper.SkipConstructor<MobileParty>();
        SetupId(defeated, "defeated-1");

        handler.Handle_PartyDestroyed(Payload(victoriousPartyBase: null, defeated));

        var sent = Assert.IsType<NetworkApplyDestroyParty>(sentMessage!);
        Assert.Null(sent.VictoriousPartyId);
        Assert.Equal("defeated-1", sent.DefeatedPartyId);
        messageBroker.Verify(
            b => b.Publish(It.IsAny<object>(), It.IsAny<InstanceDestroyed<MobileParty>>()),
            Times.Never);
    }

    [Fact]
    public void Handle_PartyDestroyed_ResolvableVictoriousParty_ReplicatesBothIds()
    {
        var victor = ObjectHelper.SkipConstructor<PartyBase>();
        var defeated = ObjectHelper.SkipConstructor<MobileParty>();
        SetupId(victor, "victor-1");
        SetupId(defeated, "defeated-1");

        handler.Handle_PartyDestroyed(Payload(victor, defeated));

        var sent = Assert.IsType<NetworkApplyDestroyParty>(sentMessage!);
        Assert.Equal("victor-1", sent.VictoriousPartyId);
        Assert.Equal("defeated-1", sent.DefeatedPartyId);
        network.Verify(n => n.SendAll(It.IsAny<IMessage>()), Times.Once);
        messageBroker.Verify(
            b => b.Publish(It.IsAny<object>(), It.IsAny<InstanceDestroyed<MobileParty>>()),
            Times.Never);
    }

    [Fact]
    public void Handle_PartyDestroyed_UnresolvableDefeatedParty_DoesNotReplicate()
    {
        var defeated = ObjectHelper.SkipConstructor<MobileParty>();
        SetupNoId(defeated);

        handler.Handle_PartyDestroyed(Payload(victoriousPartyBase: null, defeated));

        Assert.Null(sentMessage);
        network.Verify(n => n.SendAll(It.IsAny<IMessage>()), Times.Never);
        messageBroker.Verify(
            b => b.Publish(It.IsAny<object>(), It.IsAny<InstanceDestroyed<MobileParty>>()),
            Times.Never);
    }

    [Fact]
    public void Handle_PartyDestroyed_NonNullUnresolvableVictoriousParty_DoesNotReplicate()
    {
        var victor = ObjectHelper.SkipConstructor<PartyBase>();
        var defeated = ObjectHelper.SkipConstructor<MobileParty>();
        SetupNoId(victor);

        handler.Handle_PartyDestroyed(Payload(victor, defeated));

        Assert.Null(sentMessage);
        network.Verify(n => n.SendAll(It.IsAny<IMessage>()), Times.Never);
    }

    [Fact]
    public void Handle_DestroyPartyRequested_ResolvableParties_SendsRequest()
    {
        var destroyer = ObjectHelper.SkipConstructor<PartyBase>();
        var defeated = ObjectHelper.SkipConstructor<MobileParty>();
        SetupId(destroyer, "destroyer-1");
        SetupId(defeated, "defeated-1");

        handler.Handle_DestroyPartyRequested(RequestPayload(destroyer, defeated));

        var sent = Assert.IsType<NetworkRequestDestroyParty>(sentMessage!);
        Assert.Equal("destroyer-1", sent.DestroyerPartyId);
        Assert.Equal("defeated-1", sent.DefeatedPartyId);
        network.Verify(n => n.SendAll(It.IsAny<IMessage>()), Times.Once);
    }

    [Fact]
    public void Handle_DestroyPartyRequested_UnresolvableDestroyer_DoesNotSend()
    {
        var destroyer = ObjectHelper.SkipConstructor<PartyBase>();
        var defeated = ObjectHelper.SkipConstructor<MobileParty>();
        SetupId(defeated, "defeated-1");
        SetupNoId(destroyer);

        handler.Handle_DestroyPartyRequested(RequestPayload(destroyer, defeated));

        Assert.Null(sentMessage);
        network.Verify(n => n.SendAll(It.IsAny<IMessage>()), Times.Never);
    }

    [Fact]
    public void Handle_DestroyPartyRequested_UnresolvableDefeated_DoesNotSend()
    {
        var destroyer = ObjectHelper.SkipConstructor<PartyBase>();
        var defeated = ObjectHelper.SkipConstructor<MobileParty>();
        SetupId(destroyer, "destroyer-1");
        SetupNoId(defeated);

        handler.Handle_DestroyPartyRequested(RequestPayload(destroyer, defeated));

        Assert.Null(sentMessage);
        network.Verify(n => n.SendAll(It.IsAny<IMessage>()), Times.Never);
    }

    private MessagePayload<DestroyPartyRequested> RequestPayload(PartyBase destroyer, MobileParty defeated) =>
        new(this, new DestroyPartyRequested(destroyer, defeated));

    private MessagePayload<DestroyPartyApplied> Payload(PartyBase? victoriousPartyBase, MobileParty defeated) =>
        new(this, new DestroyPartyApplied(victoriousPartyBase, defeated));

    private void SetupId(object party, string id)
    {
        objectManager.Setup(o => o.TryGetIdWithLogging(party, out id)).Returns(true);
        objectManager.Setup(o => o.TryGetId(party, out id)).Returns(true);
    }

    private void SetupNoId(object party)
    {
        string unused = string.Empty;
        objectManager.Setup(o => o.TryGetIdWithLogging(party, out unused)).Returns(false);
        objectManager.Setup(o => o.TryGetId(party, out unused)).Returns(false);
    }
}
