#if DEBUG
using System;
using System.Linq;
using Missions.Battles;
using Missions.Messages;
using ProtoBuf;
using Xunit;

namespace Coop.Tests.Missions.Battles;

public sealed class NavalLabNativeStateTests
{
    private readonly NavalLabNativeState state = new();
    private readonly NavalLabManifest manifest;
    public NavalLabNativeStateTests()
    {
        var id = Guid.NewGuid();
        manifest = new NavalLabManifest("naval-lab:" + id.ToString("N"), id, new[] { "A", "B" },
            Enumerable.Range(0, 10).Select(_ => Guid.NewGuid()).ToArray(), new[] { Guid.NewGuid(), Guid.NewGuid() }, NavalLabMode.TwoClientNative);
        state.Initialize(manifest);
    }
    private NetworkNavalLabStations Stations(int slot, string[]? keys = null, Guid[]? agents = null) =>
        new(manifest.IncarnationId, 1, slot, "offer", agents ?? manifest.Combatants.Skip((slot * 5) + 1).Take(4).ToArray(),
            keys ?? new[] { "left/0", "right/0", "left/1", "right/1" });
    private void Ready()
    {
        foreach (var owner in manifest.Controllers) state.Deploy(owner);
        state.Offer("A", Stations(0)); state.Offer("B", Stations(1));
        foreach (var owner in manifest.Controllers)
            for (int slot = 0; slot < 2; slot++) state.Acknowledge(owner, Stations(slot));
        Assert.True(state.Ready);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BothOwnerOrdersNeedAllFourMatchingAcks(bool reverse)
    {
        var order = reverse ? new[] { 1, 0 } : new[] { 0, 1 };
        foreach (int slot in order)
        {
            state.Deploy(manifest.Controllers[slot]);
            Assert.True(state.Offer(manifest.Controllers[slot], Stations(slot)));
            Assert.False(state.Offer(manifest.Controllers[slot], Stations(slot)));
            Assert.False(state.Ready);
        }
        foreach (int slot in order)
        {
            Assert.True(state.Acknowledge("A", Stations(slot)));
            Assert.False(state.Acknowledge("A", Stations(slot)));
            Assert.False(state.Ready);
        }
        state.Acknowledge("B", Stations(0)); Assert.False(state.Ready);
        state.Acknowledge("B", Stations(1)); Assert.True(state.Ready);
        state.Stop(); Assert.False(state.Ready);
        Assert.Throws<InvalidOperationException>(() => state.Acknowledge("B", Stations(1)));
    }
    [Theory]
    [InlineData("early")]
    [InlineData("duplicate_station")]
    [InlineData("foreign_actor")]
    [InlineData("wrong_owner")]
    [InlineData("conflict")]
    [InlineData("mismatched_ack")]
    public void InvalidStationTransactionsReject(string kind)
    {
        if (kind == "early") { Assert.Throws<InvalidOperationException>(() => state.Acknowledge("A", Stations(0))); return; }
        state.Deploy("A"); state.Deploy("B");
        if (kind == "duplicate_station") Assert.Throws<InvalidOperationException>(() => state.Offer("A", Stations(0, new[] { "x", "x", "y", "z" })));
        if (kind == "foreign_actor") Assert.Throws<InvalidOperationException>(() => state.Offer("A", Stations(0, agents: manifest.Combatants.Skip(6).Take(4).ToArray())));
        if (kind == "wrong_owner") Assert.Throws<InvalidOperationException>(() => state.Offer("B", Stations(0)));
        state.Offer("A", Stations(0)); state.Offer("B", Stations(1));
        if (kind == "conflict") Assert.Throws<InvalidOperationException>(() => state.Offer("A", Stations(0, new[] { "w", "x", "y", "z" })));
        if (kind == "mismatched_ack") Assert.Throws<InvalidOperationException>(() => state.Acknowledge("B", Stations(0, new[] { "w", "x", "y", "z" })));
        Assert.False(state.Ready);
    }
    [Fact]
    public void CompleteWireRecordsRoundTrip()
    {
        var stations = Stations(1);
        var clone = Serializer.DeepClone(stations);
        Assert.Equal(stations.Combatants, clone.Combatants); Assert.Equal(stations.Keys, clone.Keys);
        Assert.Equal(stations.IncarnationId, clone.IncarnationId); Assert.Equal(1, clone.Ship);
        var input = new NetworkNavalLabHelmInput(manifest.IncarnationId, 1, 1, 7, 99, true, -1, -1, 1, 0.25f, 2);
        var copy = Serializer.DeepClone(input);
        Assert.Equal(input.IncarnationId, copy.IncarnationId); Assert.Equal(7, copy.Sequence);
        Assert.Equal(-1, copy.Lateral); Assert.Equal(-1, copy.Longitudinal); Assert.Equal(1, copy.DoubleTap);
        Assert.Equal(0.25f, copy.Rudder); Assert.Equal(2, copy.Sail); Assert.True(copy.HasHelm); Assert.Equal(99, copy.DeadlineUtcTicks);
    }
}
#endif
