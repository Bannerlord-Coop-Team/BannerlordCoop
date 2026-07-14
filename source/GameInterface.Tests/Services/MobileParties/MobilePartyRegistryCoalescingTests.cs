using Common;
using Common.Messaging;
using Common.Network;
using Common.Network.Coalescing;
using Common.Util;
using GameInterface.Registry.Auto;
using GameInterface.Services.Entity;
using GameInterface.Services.MobileParties;
using GameInterface.Services.MobileParties.Messages;
using Moq;
using Serilog;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using Xunit;
using ObjectManagerService = GameInterface.Services.ObjectManager.ObjectManager;

namespace GameInterface.Tests.Services.MobileParties;

public sealed class MobilePartyRegistryCoalescingTests
{
    [Fact]
    public void Destroy_DropsPendingBehaviorBeforeNetworkDestroy_AndFlushHasNoLateUpdate()
    {
        const string fullPartyId = "MobileParty_destroy-me";
        const string compactPartyId = "destroy-me";

        var party = ObjectHelper.SkipConstructor<MobileParty>();
        party.Party = ObjectHelper.SkipConstructor<PartyBase>();
        party.Party.MobileParty = party;
        party.Party.MemberRoster = new TroopRoster();
        party.Party.PrisonRoster = new TroopRoster();

        var objectManager = new ObjectManagerService(Mock.Of<ILogger>());
        Assert.True(objectManager.AddExisting(fullPartyId, party));

        var coalescer = new SendCoalescer();
        coalescer.Enqueue(
            new CoalesceKey("party-behavior", compactPartyId),
            new LatestWinsPayload(new PendingBehaviorUpdate()));

        var sent = new List<IMessage>();
        var network = new Mock<INetwork>();
        network
            .Setup(instance => instance.SendAll(It.IsAny<IMessage>()))
            .Callback<IMessage>(sent.Add);

        var broker = new MessageBroker();
        bool mobilePartyDestroyedPublished = false;
        Action<MessagePayload<MobilePartyDestroyed>> destroyedSubscription =
            _ => mobilePartyDestroyedPublished = true;
        broker.Subscribe(destroyedSubscription);

        var registry = new MobilePartyRegistry(
            Mock.Of<IControllerIdProvider>(),
            broker,
            Mock.Of<ILogger>(),
            Mock.Of<IAutoRegistryFactory>(),
            objectManager,
            coalescer);
        using var handler = new AutoRegistryHandler<MobileParty>(
            registry,
            broker,
            network.Object,
            objectManager);

        broker.Publish(this, new InstanceDestroyed<MobileParty>(party));

        Assert.True(mobilePartyDestroyedPublished);
        Assert.False(coalescer.HasPending);
        var destroy = Assert.IsType<NetworkDestroyInstance<MobileParty>>(Assert.Single(sent));
        Assert.Equal(fullPartyId, destroy.InstanceId);

        coalescer.Flush(network.Object);
        Assert.Single(sent);
    }

    private readonly struct PendingBehaviorUpdate : ICommand
    {
    }
}
