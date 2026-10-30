#if COOP_NAVAL
using Common;
using Common.Messaging;
using Common.Network;
using Common.Tests.Utils;
using Common.Util;
using Coop.Naval.Storms.Data;
using Coop.Naval.Storms.Handlers;
using Coop.Naval.Storms.Interfaces;
using Coop.Naval.Storms.Messages;
using GameInterface.Services.ObjectManager;
using Moq;
using NavalDLC.Map;
using ProtoBuf;
using Serilog;
using System;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using TaleWorlds.Library;
using Xunit;

namespace Coop.Tests.Naval;

public class StormSyncHandlerTests : IDisposable
{
    private const string StormId = "Storm_Created_1";

    private readonly TestMessageBroker messageBroker = new TestMessageBroker();
    private readonly global::GameInterface.Services.ObjectManager.ObjectManager objectManager = new(Mock.Of<ILogger>());
    private readonly Mock<INetwork> network = new Mock<INetwork>();
    private readonly Mock<IStormInterface> stormInterface = new Mock<IStormInterface>();
    private readonly Storm storm = (Storm)FormatterServices.GetUninitializedObject(typeof(Storm));
    private readonly StormSyncHandler handler;

    public StormSyncHandlerTests()
    {
        handler = new StormSyncHandler(messageBroker, objectManager, network.Object, stormInterface.Object);
    }

    public void Dispose() => handler.Dispose();

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void StormStateChanged_SendsTheStateToClients(bool isNewStorm)
    {
        var state = CreateState(new[] { 1f, 2f, 3f });
        stormInterface.Setup(storms => storms.GetState(storm)).Returns(state);
        Assert.True(objectManager.AddExisting(StormId, storm));

        messageBroker.Publish(this, new StormStateChanged(storm, isNewStorm));

        network.Verify(net => net.SendAll(It.Is<NetworkStormStateChanged>(message =>
            message.StormId == StormId &&
            message.IsNewStorm == isNewStorm &&
            message.State.Intensity == state.Intensity)), Times.Once);
    }

    [Fact]
    public void StormStateChanged_UnregisteredStorm_SendsNothing()
    {
        messageBroker.Publish(this, new StormStateChanged(storm, true));

        network.Verify(net => net.SendAll(It.IsAny<IMessage>()), Times.Never);
    }

    // A new storm is added to the client's spawned list so it ticks and gets a visual; later snapshots only update it
    [Theory]
    [InlineData(true, 1)]
    [InlineData(false, 0)]
    public void NetworkStormStateChanged_AppliesTheStateWithPatchesOff(bool isNewStorm, int expectedAdds)
    {
        var state = CreateState(new[] { 1f, 2f, 3f });
        var appliedWithPatchesOff = false;
        stormInterface
            .Setup(storms => storms.ApplyState(storm, It.IsAny<StormStateData>()))
            .Callback(() => appliedWithPatchesOff = AllowedThread.IsThisThreadAllowed());
        Assert.True(objectManager.AddExisting(StormId, storm));

        messageBroker.Publish(this, new NetworkStormStateChanged(StormId, state, isNewStorm));
        DrainGameThread();

        stormInterface.Verify(storms => storms.ApplyState(storm, It.Is<StormStateData>(applied => applied.Intensity == state.Intensity)), Times.Once);
        stormInterface.Verify(storms => storms.AddSpawnedStorm(storm), Times.Exactly(expectedAdds));
        Assert.True(appliedWithPatchesOff);
    }

    [Fact]
    public void NetworkStormStateChanged_UnknownStorm_AppliesNothing()
    {
        messageBroker.Publish(this, new NetworkStormStateChanged(StormId, CreateState(new[] { 1f, 2f, 3f }), true));
        DrainGameThread();

        stormInterface.Verify(storms => storms.ApplyState(It.IsAny<Storm>(), It.IsAny<StormStateData>()), Times.Never);
        stormInterface.Verify(storms => storms.AddSpawnedStorm(It.IsAny<Storm>()), Times.Never);
    }

    [Fact]
    public void Dispose_UnsubscribesBothMessages()
    {
        handler.Dispose();

        Assert.Equal(-1, messageBroker.GetSubscriberCountForType<StormStateChanged>());
        Assert.Equal(-1, messageBroker.GetSubscriberCountForType<NetworkStormStateChanged>());
    }

    [Fact]
    public void NetworkStormStateChanged_RoundTripsEveryField()
    {
        var trail = Enumerable.Range(0, Storm.PreviousPositionsCount * 3).Select(i => i + 0.5f).ToArray();
        var state = CreateState(trail);

        var received = RoundTrip(new NetworkStormStateChanged(StormId, state, true));

        Assert.Equal(StormId, received.StormId);
        Assert.True(received.IsNewStorm);
        Assert.Equal((int)Storm.StormTypes.Hurricane, received.State.StormType);
        Assert.Equal(120.5f, received.State.PositionX);
        Assert.Equal(340.25f, received.State.PositionY);
        Assert.Equal(0.75f, received.State.Intensity);
        Assert.Equal(1.5f, received.State.Speed);
        Assert.Equal(long.MaxValue - 1, received.State.DevelopingStateFinishTicks);
        Assert.Equal(5000L, received.State.FinalizingStateStartTicks);
        Assert.Equal(0f, received.State.DesiredMoveDirectionX);
        Assert.Equal(1f, received.State.DesiredMoveDirectionY);
        Assert.Equal(0.6f, received.State.CurrentMoveDirectionX);
        Assert.Equal(0.8f, received.State.CurrentMoveDirectionY);
        Assert.Equal(trail, received.State.PreviousPositionsAndRadius);
        Assert.Equal(3, received.State.NextUpdatePreviousDataArrayIndex);
        Assert.Equal(2000L, received.State.NextUpdateTicks);
    }

    // Protobuf drops an empty array, so a storm with no trail arrives with a null one
    [Fact]
    public void NetworkStormStateChanged_EmptyTrail_AppliesAsAnEmptyTrail()
    {
        var received = RoundTrip(new NetworkStormStateChanged(StormId, CreateState(Array.Empty<float>()), false));

        var clientStorm = (Storm)FormatterServices.GetUninitializedObject(typeof(Storm));
        new StormInterface().ApplyState(clientStorm, received.State);

        Assert.NotNull(clientStorm._previousPositionsAndRadius);
        Assert.Empty(clientStorm._previousPositionsAndRadius);
    }

    [Fact]
    public void GetState_FlattensTheTrailIntoPositionAndRadiusTriples()
    {
        var serverStorm = (Storm)FormatterServices.GetUninitializedObject(typeof(Storm));
        serverStorm._previousPositionsAndRadius = new[]
        {
            new Storm.PreviousData(new Vec2(1f, 2f), 3f),
            new Storm.PreviousData(new Vec2(4f, 5f), 6f),
        };

        var state = new StormInterface().GetState(serverStorm);

        Assert.Equal(new[] { 1f, 2f, 3f, 4f, 5f, 6f }, state.PreviousPositionsAndRadius);
    }

    private static StormStateData CreateState(float[] trail) => new StormStateData(
        (int)Storm.StormTypes.Hurricane,
        120.5f,
        340.25f,
        0.75f,
        1.5f,
        long.MaxValue - 1,
        5000L,
        0f,
        1f,
        0.6f,
        0.8f,
        trail,
        3,
        2000L);

    private static T RoundTrip<T>(T message)
    {
        using var stream = new MemoryStream();
        Serializer.Serialize(stream, message);
        stream.Position = 0;
        return Serializer.Deserialize<T>(stream);
    }

    private static void DrainGameThread() => GameThread.Run(() => { }, blocking: true);
}
#endif
