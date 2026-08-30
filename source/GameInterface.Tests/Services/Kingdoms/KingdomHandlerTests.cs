using Common.Messaging;
using Common.Util;
using GameInterface.Services.Kingdoms;
using GameInterface.Services.Kingdoms.Handlers;
using GameInterface.Services.Kingdoms.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using Moq;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.Library;
using Xunit;
using CampaignKingdomDecision = TaleWorlds.CampaignSystem.Election.KingdomDecision;

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

    [Fact]
    public void RemoveDecision_ClearsStateBeforeClosingAndRemovingDecision()
    {
        var kingdom = ObjectHelper.SkipConstructor<Kingdom>();
        var decision = ObjectHelper.SkipConstructor<DeclareWarDecision>();
        kingdom._unresolvedDecisions = new MBList<CampaignKingdomDecision> { decision };
        var objectManager = new Mock<IObjectManager>();
        Kingdom resolvedKingdom = kingdom;
        objectManager.Setup(manager => manager.TryGetObject("kingdom-id", out resolvedKingdom)).Returns(true);
        var voteManager = new Mock<IKingdomDecisionVoteManager>();
        var kingdomInterface = new Mock<IKingdomInterface>();
        var calls = new List<string>();
        voteManager.Setup(manager => manager.ClearDecisionState("kingdom-id", 0))
            .Callback(() => calls.Add("clear"));
        voteManager.Setup(manager => manager.CloseDecision("kingdom-id", 0))
            .Callback(() => calls.Add("close"));
        kingdomInterface.Setup(manager => manager.RemoveDecision(kingdom, decision))
            .Callback(() => calls.Add("remove"));
        Action<MessagePayload<RemoveDecision>> handler = CreateRemoveDecisionHandler(
            objectManager.Object,
            voteManager.Object,
            kingdomInterface.Object);

        handler(new MessagePayload<RemoveDecision>(this, new RemoveDecision("kingdom-id", 0)));

        Assert.Equal(new[] { "clear", "close", "remove" }, calls);
        voteManager.Verify(manager => manager.ClearDecisionState("kingdom-id", 0), Times.Once);
        voteManager.Verify(manager => manager.CloseDecision("kingdom-id", 0), Times.Once);
        kingdomInterface.Verify(manager => manager.RemoveDecision(kingdom, decision), Times.Once);
    }

    [Fact]
    public void RemoveDecision_MissingKingdom_ClearsStateOnce()
    {
        var objectManager = new Mock<IObjectManager>();
        Kingdom missingKingdom = null!;
        objectManager.Setup(manager => manager.TryGetObject("kingdom-id", out missingKingdom)).Returns(false);
        var voteManager = new Mock<IKingdomDecisionVoteManager>();
        var kingdomInterface = new Mock<IKingdomInterface>();
        Action<MessagePayload<RemoveDecision>> handler = CreateRemoveDecisionHandler(
            objectManager.Object,
            voteManager.Object,
            kingdomInterface.Object);

        handler(new MessagePayload<RemoveDecision>(this, new RemoveDecision("kingdom-id", 0)));

        voteManager.Verify(manager => manager.ClearDecisionState("kingdom-id", 0), Times.Once);
        kingdomInterface.Verify(
            manager => manager.RemoveDecision(It.IsAny<Kingdom>(), It.IsAny<CampaignKingdomDecision>()),
            Times.Never);
    }

    [Fact]
    public void RemoveDecision_NullDecisionList_ClearsStateOnce()
    {
        var kingdom = ObjectHelper.SkipConstructor<Kingdom>();
        kingdom._unresolvedDecisions = null;
        var objectManager = new Mock<IObjectManager>();
        Kingdom resolvedKingdom = kingdom;
        objectManager.Setup(manager => manager.TryGetObject("kingdom-id", out resolvedKingdom)).Returns(true);
        var voteManager = new Mock<IKingdomDecisionVoteManager>();
        var kingdomInterface = new Mock<IKingdomInterface>();
        Action<MessagePayload<RemoveDecision>> handler = CreateRemoveDecisionHandler(
            objectManager.Object,
            voteManager.Object,
            kingdomInterface.Object);

        handler(new MessagePayload<RemoveDecision>(this, new RemoveDecision("kingdom-id", 0)));

        voteManager.Verify(manager => manager.ClearDecisionState("kingdom-id", 0), Times.Once);
        kingdomInterface.Verify(
            manager => manager.RemoveDecision(It.IsAny<Kingdom>(), It.IsAny<CampaignKingdomDecision>()),
            Times.Never);
    }

    [Fact]
    public void RemoveDecision_OutOfRange_ClearsStateOnce()
    {
        var kingdom = ObjectHelper.SkipConstructor<Kingdom>();
        kingdom._unresolvedDecisions = new MBList<CampaignKingdomDecision>();
        var objectManager = new Mock<IObjectManager>();
        Kingdom resolvedKingdom = kingdom;
        objectManager.Setup(manager => manager.TryGetObject("kingdom-id", out resolvedKingdom)).Returns(true);
        var voteManager = new Mock<IKingdomDecisionVoteManager>();
        var kingdomInterface = new Mock<IKingdomInterface>();
        Action<MessagePayload<RemoveDecision>> handler = CreateRemoveDecisionHandler(
            objectManager.Object,
            voteManager.Object,
            kingdomInterface.Object);

        handler(new MessagePayload<RemoveDecision>(this, new RemoveDecision("kingdom-id", 1)));

        voteManager.Verify(manager => manager.ClearDecisionState("kingdom-id", 1), Times.Once);
        kingdomInterface.Verify(
            manager => manager.RemoveDecision(It.IsAny<Kingdom>(), It.IsAny<CampaignKingdomDecision>()),
            Times.Never);
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

    private static Action<MessagePayload<RemoveDecision>> CreateRemoveDecisionHandler(
        IObjectManager objectManager,
        IKingdomDecisionVoteManager voteManager,
        IKingdomInterface kingdomInterface)
    {
        Action<MessagePayload<RemoveDecision>> removeDecisionHandler = null!;
        var messageBroker = new Mock<IMessageBroker>();
        messageBroker
            .Setup(broker => broker.Subscribe(It.IsAny<Action<MessagePayload<RemoveDecision>>>()!))
            .Callback<Action<MessagePayload<RemoveDecision>>>(handler => removeDecisionHandler = handler);
        _ = new KingdomHandler(
            messageBroker.Object,
            objectManager,
            new Mock<IPlayerManager>().Object,
            voteManager,
            new Mock<IKingdomMembershipState>().Object,
            kingdomInterface,
            new Mock<IKingdomCreator>().Object);
        return removeDecisionHandler;
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
