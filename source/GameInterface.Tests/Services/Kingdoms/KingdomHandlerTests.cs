using Common.Messaging;
using Common.Util;
using GameInterface.Services.Kingdoms;
using GameInterface.Services.Kingdoms.Handlers;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using Moq;
using System;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using Xunit;

namespace GameInterface.Tests.Services.Kingdoms;

public class KingdomHandlerTests
{
    [Fact]
    public void TryGetCulture_UsesObjectManager()
    {
        var culture = ObjectHelper.SkipConstructor<CultureObject>();
        var objectManager = new Mock<IObjectManager>();
        CultureObject resolvedCulture = culture;
        objectManager.Setup(manager => manager.TryGetObject("culture-id", out resolvedCulture)).Returns(true);
        var handler = CreateHandler(objectManager.Object);

        bool result = TryGetCulture(handler, "culture-id", out CultureObject actualCulture);

        Assert.True(result);
        Assert.Same(culture, actualCulture);
        objectManager.Verify(manager => manager.TryGetObject("culture-id", out resolvedCulture), Times.Once);
    }

    [Fact]
    public void TryGetCulture_ReturnsFalseWhenObjectManagerCannotResolveCulture()
    {
        var objectManager = new Mock<IObjectManager>();
        CultureObject missingCulture = null!;
        objectManager.Setup(manager => manager.TryGetObject("culture-id", out missingCulture)).Returns(false);
        var handler = CreateHandler(objectManager.Object);

        bool result = TryGetCulture(handler, "culture-id", out CultureObject culture);

        Assert.False(result);
        Assert.Null(culture);
        objectManager.Verify(manager => manager.TryGetObject("culture-id", out missingCulture), Times.Once);
    }

    [Fact]
    public void CanChangeKingdomName_NullClan_ReturnsFalse()
    {
        var kingdom = ObjectHelper.SkipConstructor<Kingdom>();
        bool result = KingdomHandler.CanChangeKingdomName(
            null!,
            kingdom,
            "New Kingdom",
            out string reason);

        Assert.False(result);
        Assert.Equal("clan was null", reason);
    }

    [Fact]
    public void CanChangeKingdomName_NullKingdom_ReturnsFalse()
    {
        var clan = ObjectHelper.SkipConstructor<Clan>();
        bool result = KingdomHandler.CanChangeKingdomName(
            clan,
            null!,
            "New Kingdom",
            out string reason);
        
        Assert.False(result);
        Assert.Equal("kingdom was null", reason);
    }

    [Fact]
    public void CanChangeKingdomName_ClanIsNotMember_ReturnsFalse()
    {
        var clan = ObjectHelper.SkipConstructor<Clan>();
        var kingdom = ObjectHelper.SkipConstructor<Kingdom>();
        
        bool result = KingdomHandler.CanChangeKingdomName(
            clan,
            kingdom,
            "New Kingdom",
            out string reason);
        
        Assert.False(result);
        Assert.Equal("clan is not a member of the kingdom", reason);
    }

    [Fact]
    public void CanChangeKingdomName_ClanIsNotRuler_ReturnsFalse()
    {
        var clan = ObjectHelper.SkipConstructor<Clan>();
        var otherClan = ObjectHelper.SkipConstructor<Clan>();
        var kingdom = ObjectHelper.SkipConstructor<Kingdom>();

        clan._kingdom = kingdom;
        kingdom._rulingClan = otherClan;
        
        bool result = KingdomHandler.CanChangeKingdomName(
            clan,
            kingdom,
            "New Kingdom",
            out string reason);
        
        Assert.False(result);
        Assert.Equal("clan is not the ruling clan of the kingdom", reason);
    }

    [Fact]
    public void CanCahngeKingdomName_RulingClanWithName_ReturnsTrue()
    {
        var clan = ObjectHelper.SkipConstructor<Clan>();
        var kingdom = ObjectHelper.SkipConstructor<Kingdom>();
        
        clan._kingdom = kingdom;
        kingdom._rulingClan = clan;
        
        bool result = KingdomHandler.CanChangeKingdomName(
            clan,
            kingdom,
            "New Kingdom",
            out string reason);
        
        Assert.True(result);
        Assert.Null(reason);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void CanChangeKingdomName_EmptyName_ReturnsFalse(string? requestedName)
    {
        var clan  = ObjectHelper.SkipConstructor<Clan>();
        var kingdom = ObjectHelper.SkipConstructor<Kingdom>();

        clan._kingdom = kingdom;
        kingdom._rulingClan = clan;
        
        bool result = KingdomHandler.CanChangeKingdomName(
            clan,
            kingdom,
            requestedName!,
            out string reason);
        
        Assert.False(result);
        Assert.Equal("kingdom name was empty", reason);
    }
    
    private static KingdomHandler CreateHandler(IObjectManager objectManager)
    {
        return new KingdomHandler(
            new Mock<IMessageBroker>().Object,
            objectManager,
            new Mock<IPlayerManager>().Object,
            new Mock<IKingdomDecisionVoteManager>().Object,
            new Mock<IKingdomMembershipState>().Object,
            new Mock<IKingdomInterface>().Object,
            new Mock<IKingdomCreator>().Object);
    }

    private static bool TryGetCulture(KingdomHandler handler, string cultureId, out CultureObject culture)
    {
        MethodInfo methodInfo = typeof(KingdomHandler).GetMethod("TryGetCulture", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new NullReferenceException("TryGetCulture method was not found.");
        object[] args = { cultureId, null! };
        bool result = (bool)(methodInfo.Invoke(handler, args) ?? false);
        culture = (CultureObject)args[1];
        return result;
    }
}
