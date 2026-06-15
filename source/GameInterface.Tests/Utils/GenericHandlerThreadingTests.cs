using Common;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.ObjectManager;
using GameInterface.Utils;
using GameInterface.Utils.NetworkEvents;
using Moq;
using System;
using System.Runtime.CompilerServices;
using System.Threading;
using Xunit;

namespace GameInterface.Tests.Utils;

/// <summary>
/// Verifies that <see cref="GenericHandler{THandler, TInstance}"/> marshals DynamicSync applies onto
/// the game-loop thread instead of running them on the network (poller) thread that publishes the
/// message. Running a vanilla setter on the poller thread races the game loop and is the root cause
/// behind a class of intermittent "Collection was modified" / AccessViolation crashes.
/// </summary>
/// <remarks>
/// A marshaled apply only differs observably from inline execution when the publishing thread is NOT
/// the game-loop thread, so each test publishes from the xUnit worker thread and asserts the apply
/// ran on a different thread. Referencing a <see cref="Coop.Tests"/> type in the static constructor
/// forces that assembly's <c>TestGameLoopPump</c> module initializer to start (and ready) a dedicated
/// game-loop thread, so a non-blocking marshaled apply is actually drained during the test.
/// </remarks>
public class GenericHandlerThreadingTests
{
    static GenericHandlerThreadingTests()
    {
        // Coop.Tests starts and continuously pumps a dedicated game-loop thread from a
        // [ModuleInitializer] (TestGameLoopPump). Force that module initializer to run so the pump is
        // guaranteed up even when this class runs in isolation (a filtered run won't have loaded
        // Coop.Tests through any other test, and merely referencing a type does not run its
        // initializer). RunModuleConstructor is idempotent, so a full run that already started the
        // pump is unaffected.
        RuntimeHelpers.RunModuleConstructor(typeof(Coop.Tests.Mocks.TestNetwork).Module.ModuleHandle);
    }

    private static readonly TimeSpan ApplyTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Captures the GameLoopRunner's game-loop thread id by running a probe on it. The probe is
    /// blocking, so it has completed (and recorded the id) by the time this returns.
    /// </summary>
    private static int GetGameLoopThreadId()
    {
        int gameLoopThreadId = 0;
        GameLoopRunner.RunOnMainThread(() => gameLoopThreadId = Environment.CurrentManagedThreadId, blocking: true);
        return gameLoopThreadId;
    }

    [Fact]
    public void SubscribeNetwork_MarshalsApplyOntoGameLoopThread()
    {
        Assert.True(GameLoopRunner.Instance.IsInitialized, "game-loop pump was not initialized");

        var instance = new TestInstance();
        var objectManager = new Mock<IObjectManager>();
        objectManager.Setup(o => o.TryGetObjectWithLogging("inst-1", out instance)).Returns(true);

        Action<MessagePayload<TestValueEvent>> subscriber = null;
        var messageBroker = new Mock<IMessageBroker>();
        messageBroker
            .Setup(b => b.Subscribe(It.IsAny<Action<MessagePayload<TestValueEvent>>>()))
            .Callback<Action<MessagePayload<TestValueEvent>>>(s => subscriber = s);

        TestInstance appliedInstance = null;
        int appliedThreadId = 0;
        var applied = new ManualResetEventSlim(false);

        var handler = new TestHandler(messageBroker.Object, objectManager.Object, new Mock<INetwork>().Object);
        handler.WireValue((inst, msg) =>
        {
            appliedInstance = inst;
            appliedThreadId = Environment.CurrentManagedThreadId;
            applied.Set();
        });

        Assert.NotNull(subscriber);

        int publishThreadId = Environment.CurrentManagedThreadId;
        int gameLoopThreadId = GetGameLoopThreadId();
        // The publisher must not itself be the game-loop thread, otherwise RunOnMainThread runs inline
        // and the marshaling would be unobservable.
        Assert.NotEqual(publishThreadId, gameLoopThreadId);

        subscriber(new MessagePayload<TestValueEvent>(this, new TestValueEvent("inst-1")));

        Assert.True(applied.Wait(ApplyTimeout), "apply did not run on the game-loop thread within the timeout");
        Assert.Same(instance, appliedInstance);
        Assert.Equal(gameLoopThreadId, appliedThreadId);
    }

    [Fact]
    public void SubscribeNetworkReference_MarshalsApplyOntoGameLoopThread()
    {
        Assert.True(GameLoopRunner.Instance.IsInitialized, "game-loop pump was not initialized");

        var instance = new TestInstance();
        var value = new TestReference();
        var objectManager = new Mock<IObjectManager>();
        objectManager.Setup(o => o.TryGetObjectWithLogging("inst-1", out instance)).Returns(true);
        objectManager.Setup(o => o.TryGetObjectWithLogging("val-1", out value)).Returns(true);

        Action<MessagePayload<TestReferenceEvent>> subscriber = null;
        var messageBroker = new Mock<IMessageBroker>();
        messageBroker
            .Setup(b => b.Subscribe(It.IsAny<Action<MessagePayload<TestReferenceEvent>>>()))
            .Callback<Action<MessagePayload<TestReferenceEvent>>>(s => subscriber = s);

        TestInstance appliedInstance = null;
        TestReference appliedValue = null;
        int appliedThreadId = 0;
        var applied = new ManualResetEventSlim(false);

        var handler = new TestHandler(messageBroker.Object, objectManager.Object, new Mock<INetwork>().Object);
        handler.WireReference((inst, val, msg) =>
        {
            appliedInstance = inst;
            appliedValue = val;
            appliedThreadId = Environment.CurrentManagedThreadId;
            applied.Set();
        });

        Assert.NotNull(subscriber);

        int publishThreadId = Environment.CurrentManagedThreadId;
        int gameLoopThreadId = GetGameLoopThreadId();
        // The publisher must not itself be the game-loop thread, otherwise RunOnMainThread runs inline
        // and the marshaling would be unobservable.
        Assert.NotEqual(publishThreadId, gameLoopThreadId);

        subscriber(new MessagePayload<TestReferenceEvent>(this, new TestReferenceEvent("inst-1", "val-1")));

        Assert.True(applied.Wait(ApplyTimeout), "apply did not run on the game-loop thread within the timeout");
        Assert.Same(instance, appliedInstance);
        Assert.Same(value, appliedValue);
        Assert.Equal(gameLoopThreadId, appliedThreadId);
    }

    private class TestInstance { }

    private class TestReference { }

    private record TestValueEvent : GenericNetworkEvent<TestInstance, int>
    {
        public override string InstanceId { get; set; }

        public TestValueEvent(string instanceId) : base(instanceId) { }
    }

    private record TestReferenceEvent : GenericNetworkReferenceEvent<TestInstance, TestReference>
    {
        public override string InstanceId { get; set; }
        public override string ValueId { get; set; }

        public TestReferenceEvent(string instanceId, string valueId) : base(instanceId, valueId) { }
    }

    private class TestHandler : GenericHandler<TestHandler, TestInstance>
    {
        public TestHandler(IMessageBroker messageBroker, IObjectManager objectManager, INetwork network)
            : base(messageBroker, objectManager, network)
        {
        }

        public void WireValue(Action<TestInstance, TestValueEvent> handler)
            => SubscribeNetwork<int, TestValueEvent>(handler);

        public void WireReference(Action<TestInstance, TestReference, TestReferenceEvent> handler)
            => SubscribeNetworkReference<TestReference, TestReferenceEvent>(handler);
    }
}
