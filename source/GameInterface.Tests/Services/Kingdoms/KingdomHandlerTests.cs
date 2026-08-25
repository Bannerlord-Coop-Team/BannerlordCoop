using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.Kingdoms;
using GameInterface.Services.Kingdoms.Data;
using GameInterface.Services.Kingdoms.Handlers;
using GameInterface.Services.Kingdoms.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using Moq;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.Library;
using Xunit;
using CampaignKingdomDecision = TaleWorlds.CampaignSystem.Election.KingdomDecision;

namespace GameInterface.Tests.Services.Kingdoms;

public class KingdomHandlerTests
{
    private const string KingdomId = "kingdom-id";
    private const string ProposerClanId = "clan-id";
    private const string TargetKingdomId = "target-kingdom-id";

    static KingdomHandlerTests()
    {
        // Coop.Tests starts and continuously pumps a dedicated game-loop thread from a [ModuleInitializer]
        // (TestGameLoopPump); force that initializer to run so the pump is up when this class runs in
        // isolation. RunModuleConstructor is idempotent.
        RuntimeHelpers.RunModuleConstructor(typeof(Coop.Tests.Mocks.TestNetwork).Module.ModuleHandle);
    }

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
    
    [Fact]
    public void AddDecision_ResolvesEveryLookupOnTheGameThread()
    {
        Assert.True(GameThread.Instance.IsInitialized, "game-loop pump was not initialized");
        Assert.False(GameThread.Instance.IsGameThread);

        var lookupThreadIds = new List<int>();
        var objectManager = CreateDecisionObjectManager(
            () => lookupThreadIds.Add(Thread.CurrentThread.ManagedThreadId));
        var kingdomInterface = new Mock<IKingdomInterface>();
        var handler = CreateHandler<AddDecision>(objectManager, kingdomInterface.Object);

        int gameThreadId = 0;
        GameThread.Run(() => gameThreadId = Thread.CurrentThread.ManagedThreadId, blocking: true);

        handler(new MessagePayload<AddDecision>(this, CreateAddDecision(wasQueued: true)));

        Assert.NotEmpty(lookupThreadIds);
        Assert.All(lookupThreadIds, threadId => Assert.Equal(gameThreadId, threadId));
    }

    [Fact]
    public void AddDecision_ForwardsTheServerQueueAnswerToTheKingdomInterface()
    {
        var objectManager = CreateDecisionObjectManager();
        var kingdomInterface = new Mock<IKingdomInterface>();
        var handler = CreateHandler<AddDecision>(objectManager, kingdomInterface.Object);

        handler(new MessagePayload<AddDecision>(this, CreateAddDecision(wasQueued: false)));

        kingdomInterface.Verify(
            gameInterface => gameInterface.RunAddDecision(
                It.IsAny<Kingdom>(),
                It.IsAny<CampaignKingdomDecision>(),
                false,
                0.25f,
                false),
            Times.Once);
    }

    [Fact]
    public void AddDecision_MissingKingdom_DoesNotAdd()
    {
        var objectManager = new Mock<IObjectManager>();
        Kingdom missingKingdom = null!;
        objectManager.Setup(manager => manager.TryGetObjectWithLogging(KingdomId, out missingKingdom)).Returns(false);
        var kingdomInterface = new Mock<IKingdomInterface>();
        var handler = CreateHandler<AddDecision>(objectManager.Object, kingdomInterface.Object);

        handler(new MessagePayload<AddDecision>(this, CreateAddDecision(wasQueued: true)));

        kingdomInterface.Verify(
            gameInterface => gameInterface.RunAddDecision(
                It.IsAny<Kingdom>(),
                It.IsAny<CampaignKingdomDecision>(),
                It.IsAny<bool>(),
                It.IsAny<float>(),
                It.IsAny<bool?>()),
            Times.Never);
    }

    private static AddDecision CreateAddDecision(bool wasQueued)
    {
        var data = new DeclareWarDecisionData(ProposerClanId, KingdomId, 0, false, false, false, TargetKingdomId);
        return new AddDecision(KingdomId, data, false, 0.25f, wasQueued);
    }

    /// <summary>
    /// Resolves everything <see cref="DeclareWarDecisionData.TryGetKingdomDecision"/> needs, optionally
    /// reporting each lookup so a test can observe which thread it ran on.
    /// </summary>
    private static IObjectManager CreateDecisionObjectManager(Action onLookup = null)
    {
        var objectManager = new Mock<IObjectManager>();
        Kingdom kingdom = ObjectHelper.SkipConstructor<Kingdom>();
        Kingdom targetKingdom = ObjectHelper.SkipConstructor<Kingdom>();
        Clan proposerClan = ObjectHelper.SkipConstructor<Clan>();

        objectManager.Setup(manager => manager.TryGetObjectWithLogging(KingdomId, out kingdom))
            .Callback(() => onLookup?.Invoke())
            .Returns(true);
        objectManager.Setup(manager => manager.TryGetObject(KingdomId, out kingdom))
            .Callback(() => onLookup?.Invoke())
            .Returns(true);
        objectManager.Setup(manager => manager.TryGetObject(TargetKingdomId, out targetKingdom))
            .Callback(() => onLookup?.Invoke())
            .Returns(true);
        objectManager.Setup(manager => manager.TryGetObject(ProposerClanId, out proposerClan))
            .Callback(() => onLookup?.Invoke())
            .Returns(true);
        return objectManager.Object;
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
        return CreateHandler<RemoveDecision>(objectManager, kingdomInterface, voteManager);
    }

    /// <summary>
    /// Builds a <see cref="KingdomHandler"/> against mocked collaborators and returns the delegate it
    /// subscribed for <typeparamref name="T"/>, so a test can drive that one handler directly.
    /// </summary>
    private static Action<MessagePayload<T>> CreateHandler<T>(
        IObjectManager objectManager,
        IKingdomInterface kingdomInterface,
        IKingdomDecisionVoteManager voteManager = null) where T : IMessage
    {
        Action<MessagePayload<T>> subscribedHandler = null!;
        var messageBroker = new Mock<IMessageBroker>();
        messageBroker
            .Setup(broker => broker.Subscribe(It.IsAny<Action<MessagePayload<T>>>()!))
            .Callback<Action<MessagePayload<T>>>(handler => subscribedHandler = handler);
        _ = new KingdomHandler(
            messageBroker.Object,
            objectManager,
            new Mock<IPlayerManager>().Object,
            voteManager ?? new Mock<IKingdomDecisionVoteManager>().Object,
            new Mock<IKingdomMembershipState>().Object,
            kingdomInterface,
            new Mock<IKingdomCreator>().Object);
        return subscribedHandler;
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
