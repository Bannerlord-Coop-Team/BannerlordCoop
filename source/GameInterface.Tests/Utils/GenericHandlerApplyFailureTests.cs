using Common;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.ObjectManager;
using GameInterface.Utils;
using GameInterface.Utils.NetworkEvents;
using Moq;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Xunit;

namespace GameInterface.Tests.Utils;

public class GenericHandlerApplyFailureTests
{
    static GenericHandlerApplyFailureTests()
    {
        // Starts the Coop.Tests game-loop pump so the marshaled applies are drained, see GenericHandlerThreadingTests
        RuntimeHelpers.RunModuleConstructor(typeof(Coop.Tests.Mocks.TestNetwork).Module.ModuleHandle);
    }

    [Fact]
    public void FailingApply_NamesEachObjectOnce()
    {
        var first = new TestInstance();
        var second = new TestInstance();
        var objectManager = new Mock<IObjectManager>();
        objectManager.Setup(o => o.TryGetObjectWithLogging(1u, out first)).Returns(true);
        objectManager.Setup(o => o.TryGetObjectWithLogging(2u, out second)).Returns(true);
        string firstId = "CharacterObject_7685";
        string secondId = "Created_17174";
        objectManager.Setup(o => o.TryGetId(first, out firstId)).Returns(true);
        objectManager.Setup(o => o.TryGetId(second, out secondId)).Returns(true);

        var sink = new CollectingSink();
        var subscriber = WireHandler(objectManager.Object, sink, (inst, msg) => throw new NullReferenceException());

        subscriber(new MessagePayload<TestValueEvent>(this, new TestValueEvent(1)));
        subscriber(new MessagePayload<TestValueEvent>(this, new TestValueEvent(1)));
        subscriber(new MessagePayload<TestValueEvent>(this, new TestValueEvent(2)));
        DrainGameThread();

        var failures = sink.Events.Select(e => e.RenderMessage()).ToList();
        Assert.Equal(2, failures.Count);
        Assert.Single(failures, m => m.Contains(firstId) && m.Contains("handle 1"));
        Assert.Single(failures, m => m.Contains(secondId) && m.Contains("handle 2"));
    }

    [Fact]
    public void SucceedingApply_LogsNothing()
    {
        var instance = new TestInstance();
        var objectManager = new Mock<IObjectManager>();
        objectManager.Setup(o => o.TryGetObjectWithLogging(1u, out instance)).Returns(true);

        int applied = 0;
        var sink = new CollectingSink();
        var subscriber = WireHandler(objectManager.Object, sink, (inst, msg) => applied++);

        subscriber(new MessagePayload<TestValueEvent>(this, new TestValueEvent(1)));
        DrainGameThread();

        Assert.Equal(1, applied);
        Assert.Empty(sink.Events);
    }

    private static Action<MessagePayload<TestValueEvent>> WireHandler(
        IObjectManager objectManager, CollectingSink sink, Action<TestInstance, TestValueEvent> apply)
    {
        Action<MessagePayload<TestValueEvent>> subscriber = null;
        var messageBroker = new Mock<IMessageBroker>();
        messageBroker
            .Setup(b => b.Subscribe(It.IsAny<Action<MessagePayload<TestValueEvent>>>()))
            .Callback<Action<MessagePayload<TestValueEvent>>>(s => subscriber = s);

        var logger = new LoggerConfiguration().MinimumLevel.Verbose().WriteTo.Sink(sink).CreateLogger();
        var handler = new TestHandler(messageBroker.Object, objectManager, new Mock<INetwork>().Object, logger);
        handler.WireValue(apply);

        Assert.NotNull(subscriber);
        return subscriber;
    }

    // The game thread queue is FIFO, so a blocking probe returns after every earlier apply ran
    private static void DrainGameThread()
    {
        Assert.True(GameThread.Instance.IsInitialized, "game-loop pump was not initialized");
        GameThread.Run(() => { }, blocking: true);
    }

    private sealed class CollectingSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = new List<LogEvent>();
        public void Emit(LogEvent logEvent) => Events.Add(logEvent);
    }

    private class TestInstance { }

    private record TestValueEvent : GenericNetworkEvent<TestInstance, int>
    {
        public override uint InstanceId { get; set; }

        public TestValueEvent(uint instanceId) : base(instanceId) { }
    }

    private class TestHandler : GenericHandler<TestHandler, TestInstance>
    {
        public TestHandler(IMessageBroker messageBroker, IObjectManager objectManager, INetwork network, ILogger logger)
            : base(messageBroker, objectManager, network, logger)
        {
        }

        public void WireValue(Action<TestInstance, TestValueEvent> handler)
            => SubscribeNetwork<int, TestValueEvent>(handler);
    }
}
