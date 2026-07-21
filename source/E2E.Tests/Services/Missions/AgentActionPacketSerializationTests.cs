using Common.PacketHandlers;
using Common.Serialization;
using Missions.Agents.Packets;
using System;
using Xunit;

namespace E2E.Tests.Services.Missions;

public class AgentActionPacketSerializationTests
{
    [Fact]
    public void AgentActionPacket_RoundTripsBattleHostEpoch()
    {
        var original = new AgentActionPacket(
            "battle-host",
            Array.Empty<Guid>(),
            Array.Empty<AgentActionData>(),
            Array.Empty<long>(),
            battleHostEpoch: 7);
        var serializer = new ProtoBufSerializer(new SerializableTypeMapper());

        byte[] wire = serializer.Serialize(original);
        var result = Assert.IsType<AgentActionPacket>(
            serializer.Deserialize<IPacket>(wire));

        Assert.Equal("battle-host", result.ControllerId);
        Assert.Equal(7, result.BattleHostEpoch);
    }
}
