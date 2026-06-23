using Common;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Registry.Auto;
using GameInterface.Services.MobileParties.Handlers;
using GameInterface.Services.MobileParties.Messages.Lifetime;
using GameInterface.Services.ObjectManager;
using Moq;
using System;
using System.Runtime.CompilerServices;
using System.Threading;
using TaleWorlds.CampaignSystem.Party;
using Xunit;

namespace GameInterface.Tests.Services.MobileParties;

/// <summary>
/// Unit tests for the server send-side of party destruction replication in
/// <see cref="PartyLifetimeHandler"/>, plus the receive-side threading guarantee: the network
/// (poller) thread must defer object resolution and the <c>IsActive</c> guard onto the game-loop
/// thread, so a party resolved on the poller thread cannot go stale before the action applies.
/// </summary>
/// <remarks>
/// The threading tests reuse <see cref="Coop.Tests"/>'s <c>TestGameLoopPump</c>, started from a
/// <c>[ModuleInitializer]</c>, to run a real game-loop thread (mirroring
/// <see cref="GameInterface.Tests.Utils.GenericHandlerThreadingTests"/>). A marshaled apply only
/// differs observably from inline execution when the publishing thread is NOT the game-loop thread,
/// so each test publishes from the xUnit worker thread and asserts the resolution ran on the
/// game-loop thread instead.
/// </remarks>
public class PartyLifetimeHandlerTests
{
    static PartyLifetimeHandlerTests()
    {
        // Force Coop.Tests' TestGameLoopPump module initializer to run so the dedicated game-loop
        // thread is up even when this class runs in isolation (a filtered run won't have loaded
        // Coop.Tests through any other test, and merely referencing a type does not run its
        // initializer). RunModuleConstructor is idempotent.
        RuntimeHelpers.RunModuleConstructor(typeof(Coop.Tests.Mocks.TestNetwork).Module.ModuleHandle);
    }

    private static readonly TimeSpan ApplyTimeout = TimeSpan.FromSeconds(10);

    private readonly Mock<IMessageBroker> messageBroker = new();
    private readonly Mock<IObjectManager> objectManager = new();
    private readonly Mock<INetwork> network = new();
    private readonly PartyLifetimeHandler handler;

    private object? sentMessage;

    private Action<MessagePayload<NetworkApplyDestroyParty>>? destroyPartyReceiver;
    private Action<MessagePayload<NetworkPartyDisbanded>>? partyDisbandedReceiver;

    public PartyLifetimeHandlerTests()
    {
        // Capture the receive-path subscribers so the threading tests can drive them through the
        // real subscription wiring without widening the handlers' visibility.
        messageBroker
            .Setup(b => b.Subscribe(It.IsAny<Action<MessagePayload<NetworkApplyDestroyParty>>>()))
            .Callback<Action<MessagePayload<NetworkApplyDestroyParty>>>(s => destroyPartyReceiver = s);
        messageBroker
            .Setup(b => b.Subscribe(It.IsAny<Action<MessagePayload<NetworkPartyDisbanded>>>()))
            .Callback<Action<MessagePayload<NetworkPartyDisbanded>>>(s => partyDisbandedReceiver = s);

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
            b => b.Publish(It.IsAny<object>(),
                It.Is<InstanceDestroyed<MobileParty>>(m => ReferenceEquals(m.Instance, defeated))),
            Times.Once);
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
            b => b.Publish(It.IsAny<object>(),
                It.Is<InstanceDestroyed<MobileParty>>(m => ReferenceEquals(m.Instance, defeated))),
            Times.Once);
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
    public void Handle_DestroyParty_ResolvesPartyOnGameLoopThread()
    {
        Assert.True(GameThread.Instance.IsInitialized, "game-loop pump was not initialized");
        Assert.NotNull(destroyPartyReceiver);

        var (resolvedThreadId, resolved) = CaptureResolutionThread();

        int publishThreadId = Environment.CurrentManagedThreadId;
        int gameLoopThreadId = GetGameLoopThreadId();
        // If the publisher were itself the game-loop thread, GameThread.Run would run inline and the
        // marshaling would be unobservable.
        Assert.NotEqual(publishThreadId, gameLoopThreadId);

        destroyPartyReceiver!(new MessagePayload<NetworkApplyDestroyParty>(
            this, new NetworkApplyDestroyParty("victor-1", "defeated-1")));

        Assert.True(resolved.Wait(ApplyTimeout), "resolution did not run within the timeout");
        Assert.Equal(gameLoopThreadId, resolvedThreadId.Value);
    }

    [Fact]
    public void Handle_NetworkPartyDisbanded_ResolvesPartyOnGameLoopThread()
    {
        Assert.True(GameThread.Instance.IsInitialized, "game-loop pump was not initialized");
        Assert.NotNull(partyDisbandedReceiver);

        var (resolvedThreadId, resolved) = CaptureResolutionThread();

        int publishThreadId = Environment.CurrentManagedThreadId;
        int gameLoopThreadId = GetGameLoopThreadId();
        Assert.NotEqual(publishThreadId, gameLoopThreadId);

        partyDisbandedReceiver!(new MessagePayload<NetworkPartyDisbanded>(
            this, new NetworkPartyDisbanded("disbanded-1", "settlement-1")));

        Assert.True(resolved.Wait(ApplyTimeout), "resolution did not run within the timeout");
        Assert.Equal(gameLoopThreadId, resolvedThreadId.Value);
    }

    /// <summary>
    /// Stubs the first party resolution to fail (so the real destroy/disband action is never invoked)
    /// while recording the thread it ran on. Returns the captured thread id and a handle signaled once
    /// resolution has run.
    /// </summary>
    private (StrongBox<int> ThreadId, ManualResetEventSlim Resolved) CaptureResolutionThread()
    {
        var resolvedThreadId = new StrongBox<int>(0);
        var resolved = new ManualResetEventSlim(false);

        MobileParty outParty = null!;
        objectManager
            .Setup(o => o.TryGetObjectWithLogging(It.IsAny<string>(), out outParty))
            .Returns(false)
            .Callback(() =>
            {
                resolvedThreadId.Value = Environment.CurrentManagedThreadId;
                resolved.Set();
            });

        return (resolvedThreadId, resolved);
    }

    /// <summary>
    /// Captures the game-loop thread id by running a blocking probe on it.
    /// </summary>
    private static int GetGameLoopThreadId()
    {
        int gameLoopThreadId = 0;
        GameThread.Run(() => gameLoopThreadId = Environment.CurrentManagedThreadId, blocking: true);
        return gameLoopThreadId;
    }

    private MessagePayload<DestroyPartyApplied> Payload(PartyBase? victoriousPartyBase, MobileParty defeated) =>
        new(this, new DestroyPartyApplied(victoriousPartyBase, defeated));

    private void SetupId(object party, string id) =>
        objectManager.Setup(o => o.TryGetIdWithLogging(party, out id)).Returns(true);

    private void SetupNoId(object party)
    {
        string unused = string.Empty;
        objectManager.Setup(o => o.TryGetIdWithLogging(party, out unused)).Returns(false);
    }
}
