#if DEBUG
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
    public sealed class NavalTagProbe
    {
        [ProtoMember(10)] public long? HelmRevision { get; set; }
        [ProtoMember(11)] public int? DeckShip { get; set; }
        [ProtoMember(12)] public byte[]? DeckLocal { get; set; }
    }

    [Fact]
    public void OrdinaryWorldRecord_WritesNoNavalFields()
    {
        NavalTagProbe probe = Probe(default(AgentData));

        Assert.Null(probe.HelmRevision);
        Assert.Null(probe.DeckShip);
        Assert.Null(probe.DeckLocal);
    }

    [Fact]
    public void DeckRecord_RoundTripsThroughMovementPacketBitExact()
    {
        var data = default(AgentData);
        data.NavalHelmRevision = 7;
        data.StampNavalDeck(2, new Vec3(1.25f, -3.5f, 0.75f), 1.5f);
        var serializer = new ProtoBufSerializer(new SerializableTypeMapper());

        var restored = Assert.IsType<MovementPacket>(serializer.Deserialize(
            serializer.Serialize(new MovementPacket(new[] { Guid.NewGuid() }, new[] { data }))));

        AgentData agent = Assert.Single(restored.Agents);
        Assert.Equal(7, agent.NavalHelmRevision);
        Assert.Equal(2, agent.NavalDeckShip);
        Assert.Equal(new Vec3(1.25f, -3.5f, 0.75f), agent.NavalDeckLocal);
        Assert.Equal(1.5f, agent.Speed);
        Assert.True(agent.HasValidNavalDeck);
        Assert.NotNull(Probe(data).DeckLocal);
    }

    [Fact]
    public void RecordWithoutDeckFields_DecodesAsWorld()
    {
        using var stream = new MemoryStream();
        Serializer.Serialize(stream, new NavalTagProbe { HelmRevision = 3 });
        stream.Position = 0;

        AgentData data = Serializer.Deserialize<AgentData>(stream);

        Assert.Equal(3, data.NavalHelmRevision);
        Assert.Equal(0, data.NavalDeckShip);
        Assert.Equal(Vec3.Zero, data.NavalDeckLocal);
    }

    [Theory]
    [InlineData(0, 0f, 0f)]
    [InlineData(3, 0f, 0f)]
    [InlineData(-1, 0f, 0f)]
    [InlineData(1, float.NaN, 0f)]
    [InlineData(1, float.PositiveInfinity, 0f)]
    [InlineData(1, 0f, float.NaN)]
    [InlineData(1, 0f, -1f)]
    public void InvalidDeckPose_IsNotValid(int ship, float localX, float speed)
    {
        var data = default(AgentData);
        data.StampNavalDeck(ship, new Vec3(localX, 0f, 0f), speed);

        Assert.False(data.HasValidNavalDeck);
    }

    private static NavalTagProbe Probe(AgentData data)
    {
        using var stream = new MemoryStream();
        Serializer.Serialize(stream, data);
        stream.Position = 0;
        return Serializer.Deserialize<NavalTagProbe>(stream);
    }
}
#endif
