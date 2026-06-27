using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.MobileParties.Handlers;
using GameInterface.Services.MobileParties.Messages;
using GameInterface.Services.ObjectManager;
using Moq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit;

namespace GameInterface.Tests.Services.MobileParties;

/// <summary>
/// Unit tests for the client send-side of mercenary-hire replication in
/// <see cref="MercenaryHireHandler"/>: a client-side hire is relayed to the server as a
/// <see cref="HireMercenaries"/> request carrying the resolved ids, troop count and gold cost.
/// </summary>
public class MercenaryHireHandlerTests
{
    private readonly Mock<IMessageBroker> messageBroker = new();
    private readonly Mock<IObjectManager> objectManager = new();
    private readonly Mock<INetwork> network = new();
    private readonly MercenaryHireHandler handler;

    private object? sentMessage;

    public MercenaryHireHandlerTests()
    {
        handler = new MercenaryHireHandler(messageBroker.Object, objectManager.Object, network.Object);

        network.Setup(n => n.SendAll(It.IsAny<IMessage>()))
            .Callback<IMessage>(message => sentMessage = message);
    }

    [Fact]
    public void Handle_MercenariesHired_ResolvableObjects_SendsRequestWithIdsCountAndGold()
    {
        var hero = ObjectHelper.SkipConstructor<Hero>();
        var party = ObjectHelper.SkipConstructor<MobileParty>();
        var town = ObjectHelper.SkipConstructor<Town>();
        var troop = ObjectHelper.SkipConstructor<CharacterObject>();
        hero.Gold = 1000;
        SetupId(hero, "hero-1");
        SetupId(party, "party-1");
        SetupId(town, "town-1");
        SetupId(troop, "troop-1");

        handler.Handle_MercenariesHired(Payload(hero, party, town, troop, count: 5, goldAmount: 250));

        var sent = Assert.IsType<HireMercenaries>(sentMessage!);
        Assert.Equal("hero-1", sent.MainHeroId);
        Assert.Equal("party-1", sent.MainPartyId);
        Assert.Equal("town-1", sent.TownId);
        Assert.Equal("troop-1", sent.MercenaryTroopId);
        Assert.Equal(5, sent.Count);
        Assert.Equal(250, sent.GoldAmount);
        Assert.Equal(1000, sent.HeroGold);
        network.Verify(n => n.SendAll(It.IsAny<IMessage>()), Times.Once);
    }

    [Fact]
    public void Handle_MercenariesHired_UnresolvableHero_DoesNotSend()
    {
        var hero = ObjectHelper.SkipConstructor<Hero>();
        var party = ObjectHelper.SkipConstructor<MobileParty>();
        var town = ObjectHelper.SkipConstructor<Town>();
        var troop = ObjectHelper.SkipConstructor<CharacterObject>();
        SetupNoId(hero);

        handler.Handle_MercenariesHired(Payload(hero, party, town, troop, count: 5, goldAmount: 250));

        Assert.Null(sentMessage);
        network.Verify(n => n.SendAll(It.IsAny<IMessage>()), Times.Never);
    }

    [Fact]
    public void Handle_MercenariesHired_UnresolvableTroop_DoesNotSend()
    {
        var hero = ObjectHelper.SkipConstructor<Hero>();
        var party = ObjectHelper.SkipConstructor<MobileParty>();
        var town = ObjectHelper.SkipConstructor<Town>();
        var troop = ObjectHelper.SkipConstructor<CharacterObject>();
        hero.Gold = 1000;
        SetupId(hero, "hero-1");
        SetupId(party, "party-1");
        SetupId(town, "town-1");
        SetupNoId(troop);

        handler.Handle_MercenariesHired(Payload(hero, party, town, troop, count: 5, goldAmount: 250));

        Assert.Null(sentMessage);
        network.Verify(n => n.SendAll(It.IsAny<IMessage>()), Times.Never);
    }

    [Fact]
    public void CanApplyMercenaryHire_ValidCurrentStockAndGold_ReturnsTrue()
    {
        int count = 5;
        int unitPrice = 50;
        int goldAmount = MercenaryHireHandler.GetMercenaryHireGoldAmount(count, unitPrice);

        bool canApply = MercenaryHireHandler.CanApplyMercenaryHire(
            count,
            goldAmount,
            clientHeroGold: 1000,
            serverHeroGold: 1000,
            unitPrice: unitPrice,
            availableTroopMatches: true,
            availableMercenaries: 10);

        Assert.True(canApply);
    }

    [Theory]
    [InlineData(false, 10)]
    [InlineData(true, 4)]
    public void CanApplyMercenaryHire_StaleConversationStock_ReturnsFalse(bool availableTroopMatches, int availableMercenaries)
    {
        int count = 5;
        int unitPrice = 50;
        int goldAmount = MercenaryHireHandler.GetMercenaryHireGoldAmount(count, unitPrice);

        bool canApply = MercenaryHireHandler.CanApplyMercenaryHire(
            count,
            goldAmount,
            clientHeroGold: 1000,
            serverHeroGold: 1000,
            unitPrice: unitPrice,
            availableTroopMatches,
            availableMercenaries);

        Assert.False(canApply);
    }

    [Fact]
    public void CanApplyMercenaryHire_InsufficientCurrentServerGold_ReturnsFalse()
    {
        int count = 5;
        int unitPrice = 50;
        int goldAmount = MercenaryHireHandler.GetMercenaryHireGoldAmount(count, unitPrice);

        bool canApply = MercenaryHireHandler.CanApplyMercenaryHire(
            count,
            goldAmount,
            clientHeroGold: 1000,
            serverHeroGold: goldAmount - 1,
            unitPrice: unitPrice,
            availableTroopMatches: true,
            availableMercenaries: 10);

        Assert.False(canApply);
    }

    private MessagePayload<MercenariesHired> Payload(Hero hero, MobileParty party, Town town, CharacterObject troop, int count, int goldAmount) =>
        new(this, new MercenariesHired(hero, party, town, troop, count, goldAmount));

    private void SetupId(object obj, string id) =>
        objectManager.Setup(o => o.TryGetIdWithLogging(obj, out id)).Returns(true);

    private void SetupNoId(object obj)
    {
        string unused = string.Empty;
        objectManager.Setup(o => o.TryGetIdWithLogging(obj, out unused)).Returns(false);
    }
}
