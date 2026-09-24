using Common.Messaging;
using Common.Network;
using Common.Network.Coalescing;
using Common.Tests.Utils;
using Common.Util;
using GameInterface.Services.Heroes.Messages.Collections;
using GameInterface.Services.MobileParties.Handlers;
using GameInterface.Services.MobileParties.Messages;
using GameInterface.Services.ObjectManager;
using Moq;
using Serilog;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using Xunit;

namespace GameInterface.Tests.Services.MobileParties;

/// <summary>
/// Tests the server send path that turns volunteer slot assignments into a per-tick snapshot.
/// </summary>
public class VolunteerTypesHandlerTests
{
    [Fact]
    public void ArrayUpdates_BufferOneLatestSnapshotForAllTouchedHeroes()
    {
        var broker = new TestMessageBroker();
        var objectManager = new ObjectManager(new Mock<ILogger>().Object);
        var coalescer = new SendCoalescer();
        var sent = new List<IMessage>();
        var network = new Mock<INetwork>();
        network.Setup(instance => instance.SendAll(It.IsAny<IMessage>())).Callback<IMessage>(sent.Add);
        using var handler = new VolunteerTypesHandler(broker, objectManager, network.Object, coalescer);

        var heroA = CreateHero(objectManager, "Hero_hero_a", 1);
        var heroB = CreateHero(objectManager, "Hero_hero_b", 2);
        var troop1 = CreateCharacter(objectManager, "CharacterObject_troop_1", 3);
        var troop2 = CreateCharacter(objectManager, "CharacterObject_troop_2", 4);
        var troop3 = CreateCharacter(objectManager, "CharacterObject_troop_3", 5);

        PublishAssignment(broker, heroA, troop1, 0);
        PublishAssignment(broker, heroA, troop2, 0);
        PublishAssignment(broker, heroB, troop3, 2);

        Assert.True(coalescer.HasPending);
        Assert.Empty(sent);

        coalescer.Flush(network.Object);

        var message = Assert.IsType<UpdateVolunteers>(Assert.Single(sent));
        Assert.Equal(4u, message.UpdatedVolunteerTypeIds[1][0]);
        Assert.Equal(5u, message.UpdatedVolunteerTypeIds[2][2]);
    }

    [Fact]
    public void ArrayUpdate_UnresolvedNonNullVolunteerIsSkippedButNullSerializesEmpty()
    {
        var broker = new TestMessageBroker();
        var objectManager = new ObjectManager(new Mock<ILogger>().Object);
        var coalescer = new SendCoalescer();
        var sent = new List<IMessage>();
        var network = new Mock<INetwork>();
        network.Setup(instance => instance.SendAll(It.IsAny<IMessage>())).Callback<IMessage>(sent.Add);
        using var handler = new VolunteerTypesHandler(broker, objectManager, network.Object, coalescer);

        var hero = CreateHero(objectManager, "Hero_hero_a", 1);
        hero.VolunteerTypes[0] = CreateCharacter(objectManager, "CharacterObject_troop_1", 2);
        var unresolvedTroop = ObjectHelper.SkipConstructor<CharacterObject>();

        broker.Publish(hero, new VolunteerTypesArrayUpdated(hero, unresolvedTroop, 0));

        Assert.False(coalescer.HasPending);
        Assert.Empty(sent);

        broker.Publish(hero, new VolunteerTypesArrayUpdated(hero, null, 0));
        Assert.True(coalescer.HasPending);

        coalescer.Flush(network.Object);

        var message = Assert.IsType<UpdateVolunteers>(Assert.Single(sent));
        Assert.Equal(0u, message.UpdatedVolunteerTypeIds[1][0]);
    }

    [Fact]
    public void PeriodicUpdateAndRemoval_MergeIntoLatestSnapshots()
    {
        var broker = new TestMessageBroker();
        var objectManager = new ObjectManager(new Mock<ILogger>().Object);
        var coalescer = new SendCoalescer();
        var sent = new List<IMessage>();
        var network = new Mock<INetwork>();
        network.Setup(instance => instance.SendAll(It.IsAny<IMessage>())).Callback<IMessage>(sent.Add);
        using var handler = new VolunteerTypesHandler(broker, objectManager, network.Object, coalescer);

        var heroA = CreateHero(objectManager, "Hero_hero_a", 1);
        var heroB = CreateHero(objectManager, "Hero_hero_b", 2);
        var troop1 = CreateCharacter(objectManager, "CharacterObject_troop_1", 3);
        var troop2 = CreateCharacter(objectManager, "CharacterObject_troop_2", 4);

        heroA.VolunteerTypes[0] = troop1;
        broker.Publish(heroA, new VolunteersUpdated(new Dictionary<Hero, CharacterObject[]>
        {
            [heroA] = heroA.VolunteerTypes,
        }));

        heroA.VolunteerTypes[0] = null!;
        broker.Publish(heroA, new VolunteerRemoved(heroA, 0));

        heroB.VolunteerTypes[1] = troop2;
        broker.Publish(heroB, new VolunteersUpdated(new Dictionary<Hero, CharacterObject[]>
        {
            [heroB] = heroB.VolunteerTypes,
        }));

        coalescer.Flush(network.Object);

        var message = Assert.IsType<UpdateVolunteers>(Assert.Single(sent));
        Assert.Equal(0u, message.UpdatedVolunteerTypeIds[1][0]);
        Assert.Equal(4u, message.UpdatedVolunteerTypeIds[2][1]);
    }

    private static Hero CreateHero(IObjectManager objectManager, string id, uint handle)
    {
        var hero = ObjectHelper.SkipConstructor<Hero>();
        hero.VolunteerTypes = new CharacterObject[6];
        Assert.True(objectManager.AddExisting(id, hero, handle));
        return hero;
    }

    private static CharacterObject CreateCharacter(IObjectManager objectManager, string id, uint handle)
    {
        var character = ObjectHelper.SkipConstructor<CharacterObject>();
        Assert.True(objectManager.AddExisting(id, character, handle));
        return character;
    }

    private static void PublishAssignment(TestMessageBroker broker, Hero hero, CharacterObject character, int index)
    {
        broker.Publish(hero, new VolunteerTypesArrayUpdated(hero, character, index));
        hero.VolunteerTypes[index] = character;
    }
}
