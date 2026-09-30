using Common.Messaging;
using Common.Network;
using GameInterface.AutoSync;
using GameInterface.AutoSync.Builders;
using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using TaleWorlds.CampaignSystem.MapEvents;
using GameInterface.Utils;
using GameInterface.Utils.LocalEvents;
using GameInterface.Utils.NetworkEvents;
using Moq;
using System;
using Xunit;

namespace GameInterface.Tests.Utils;

public class GenericHandlerReferenceTests
{
    [Theory]
    [InlineData(nameof(MapEvent.MapEventVisual), true)]
    [InlineData(nameof(MapEvent.StrengthOfSide), false)]
    public void FieldSubscription_FiltersHeadlessProvidersOnlyForMapEventVisual(string memberName, bool filtersHeadless)
    {
        var factory = new Mock<IAutoRegistryFactory>();
        factory.Setup(f => f.IsManaged(It.IsAny<Type>())).Returns(true);
        var builder = new AutoSyncFieldBuilder(factory.Object, new AutoSyncRegistry(), new AutoSyncConstantsBuilder());
        var field = AccessTools.Field(typeof(MapEvent), memberName);

        var result = builder.GetSubscription(new Debuggable<System.Reflection.FieldInfo>(field, false));

        Assert.Equal(filtersHeadless, result.Contains("DedicatedServer.NoOpMapEventVisualCreator+NoOpMapEventVisual"));
        Assert.Equal(filtersHeadless, result.Contains("MapEventBattleFactory+HeadlessMapEventVisual"));
        Assert.Contains($"instance.{memberName} = value", result);
    }

    [Theory]
    [InlineData(false, true, false)]
    [InlineData(true, true, true)]
    [InlineData(true, false, true)]
    [InlineData(false, false, false)]
    public void ReferenceAssignment_SkipsOnlyOptedInUnregisteredValues(bool registered, bool skip, bool sent)
    {
        var instance = new object();
        var value = new object();
        uint instanceId = 1, valueId = registered ? 2u : 0;
        var manager = new Mock<IObjectManager>();
        manager.Setup(m => m.TryGetHandleWithLogging(instance, out instanceId)).Returns(true);
        manager.Setup(m => m.TryGetHandle(value, out valueId)).Returns(registered);
        manager.Setup(m => m.TryGetHandleWithLogging(value, out valueId)).Returns(registered);
        var network = new Mock<INetwork>();
        Action<MessagePayload<LocalMessage>> subscriber = null;
        var broker = new Mock<IMessageBroker>();
        broker.Setup(b => b.Subscribe(It.IsAny<Action<MessagePayload<LocalMessage>>>()))
            .Callback<Action<MessagePayload<LocalMessage>>>(s => subscriber = s);
        using var handler = new TestHandler(broker.Object, manager.Object, network.Object);
        handler.WireReference(_ => skip);

        subscriber(new MessagePayload<LocalMessage>(this, new LocalMessage(instance, value)));

        network.Verify(n => n.SendAll(It.Is<NetworkMessage>(m => m.InstanceId == 1 && m.ValueId == 2)),
            sent ? Times.Once() : Times.Never());
        network.Verify(n => n.SendAll(It.IsAny<IMessage>()), sent ? Times.Once() : Times.Never());
        manager.Verify(m => m.TryGetHandleWithLogging(value, out valueId),
            !registered && skip ? Times.Never() : Times.Once());
    }

    [Fact]
    public void NullAssignment_SendsZeroWithoutFilteringOrResolvingValue()
    {
        var instance = new object();
        uint instanceId = 1;
        var manager = new Mock<IObjectManager>();
        manager.Setup(m => m.TryGetHandleWithLogging(instance, out instanceId)).Returns(true);
        var network = new Mock<INetwork>();
        Action<MessagePayload<LocalMessage>> subscriber = null;
        var broker = new Mock<IMessageBroker>();
        broker.Setup(b => b.Subscribe(It.IsAny<Action<MessagePayload<LocalMessage>>>()))
            .Callback<Action<MessagePayload<LocalMessage>>>(s => subscriber = s);
        using var handler = new TestHandler(broker.Object, manager.Object, network.Object);
        handler.WireReference(_ => throw new InvalidOperationException("null must not be filtered"));

        subscriber(new MessagePayload<LocalMessage>(this, new LocalMessage(instance, null)));

        network.Verify(n => n.SendAll(It.Is<NetworkMessage>(m => m.InstanceId == 1 && m.ValueId == 0)), Times.Once());
        manager.Verify(m => m.TryGetHandleWithLogging(instance, out instanceId), Times.Once());
        manager.VerifyNoOtherCalls();
    }

    public record LocalMessage : GenericEvent<object, object>
    {
        public LocalMessage(object instance, object value) : base(instance, value) { }
    }

    public record NetworkMessage : GenericNetworkReferenceEvent<object, object>
    {
        public override uint InstanceId { get; set; }
        public override uint ValueId { get; set; }
        public NetworkMessage(uint instanceId, uint valueId) : base(instanceId, valueId)
        {
            InstanceId = instanceId;
            ValueId = valueId;
        }
    }

    private sealed class TestHandler : GenericHandler<TestHandler, object>
    {
        public TestHandler(IMessageBroker broker, IObjectManager manager, INetwork network)
            : base(broker, manager, network) { }

        public void WireReference(Predicate<object> skip)
            => SubscribeGenericReference<object, LocalMessage, NetworkMessage>(skip);
    }
}
