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

    private static KingdomHandler CreateHandler(IObjectManager objectManager)
    {
        return new KingdomHandler(
            new Mock<IMessageBroker>().Object,
            objectManager,
            new Mock<IPlayerManager>().Object,
            new Mock<IKingdomDecisionVoteManager>().Object,
            new Mock<IKingdomMembershipState>().Object);
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