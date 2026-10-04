using System;
using System.IO;
using Common.Serialization;
using GameInterface.Surrogates;
using Missions.Agents.Packets;
using ProtoBuf;
using TaleWorlds.Library;
using Xunit;
using AgentData = Missions.Agents.Packets.AgentData;

namespace E2E.Tests.Services.Missions;

public sealed class AgentDataNavalDeckWireTests
{
    public AgentDataNavalDeckWireTests()
    {
        _ = new SurrogateCollection();
    }

    [ProtoContract]
    public sealed class DeckTagProbe
    {
        [ProtoMember(11)] public int? DeckShipIndex { get; set; }
        [ProtoMember(12)] public byte[]? DeckLocal { get; set; }
    }

    [Fact]
    public void OrdinaryWorldRecord_WritesNoDeckFields()
    {
        DeckTagProbe probe = Probe(default(AgentData));

        Assert.Null(probe.DeckShipIndex);
        Assert.Null(probe.DeckLocal);
    }

    [Fact]
    public void DeckRecord_RoundTripsThroughMovementPacketWithItsHullTable()
    {
        Guid firstHull = Guid.NewGuid();
        Guid secondHull = Guid.NewGuid();
        var data = default(AgentData);
        data.StampDeck(secondHull, 2, new Vec3(1.25f, -3.5f, 0.75f), 1.5f);
        var serializer = new ProtoBufSerializer(new SerializableTypeMapper());

        var restored = Assert.IsType<MovementPacket>(serializer.Deserialize(serializer.Serialize(
            new MovementPacket(new[] { Guid.NewGuid() }, new[] { data }, new[] { firstHull, secondHull }))));

        Assert.Equal(new[] { firstHull, secondHull }, restored.DeckShips);
        AgentData agent = Assert.Single(restored.Agents);
        Assert.Equal(Guid.Empty, agent.DeckShip);
        agent.ResolveDeckShip(restored.DeckShips);
        Assert.Equal(2, agent.DeckShipIndex);
        Assert.Equal(secondHull, agent.DeckShip);
        Assert.Equal(new Vec3(1.25f, -3.5f, 0.75f), agent.DeckLocal);
        Assert.Equal(1.5f, agent.Speed);
        Assert.True(agent.HasValidDeck);
        Assert.NotNull(Probe(data).DeckLocal);
    }

    [Fact]
    public void RecordWithoutDeckFields_DecodesAsWorld()
    {
        using var stream = new MemoryStream();
        Serializer.Serialize(stream, new DeckTagProbe());
        stream.Position = 0;

        AgentData data = Serializer.Deserialize<AgentData>(stream);

        Assert.Equal(0, data.DeckShipIndex);
        Assert.Equal(Vec3.Zero, data.DeckLocal);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(-1)]
    public void DeckIndexOutsideTheHullTable_ResolvesToNoHull(int index)
    {
        var data = default(AgentData);
        data.StampDeck(Guid.NewGuid(), index, Vec3.Zero, 0f);

        data.ResolveDeckShip(new[] { Guid.NewGuid(), Guid.NewGuid() });

        Assert.Equal(Guid.Empty, data.DeckShip);
        Assert.False(data.HasValidDeck);
    }

    [Theory]
    [InlineData(0, 0f, 0f)]
    [InlineData(1, float.NaN, 0f)]
    [InlineData(1, float.PositiveInfinity, 0f)]
    [InlineData(1, 0f, float.NaN)]
    [InlineData(1, 0f, -1f)]
    public void InvalidDeckPose_IsNotValid(int index, float localX, float speed)
    {
        var data = default(AgentData);
        data.StampDeck(Guid.NewGuid(), index, new Vec3(localX, 0f, 0f), speed);

        Assert.False(data.HasValidDeck);
    }

    private static DeckTagProbe Probe(AgentData data)
    {
        using var stream = new MemoryStream();
        Serializer.Serialize(stream, data);
        stream.Position = 0;
        return Serializer.Deserialize<DeckTagProbe>(stream);
    }
}
