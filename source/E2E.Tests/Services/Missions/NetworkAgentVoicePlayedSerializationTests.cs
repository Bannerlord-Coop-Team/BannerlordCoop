using Common.Messaging;
using Common.PacketHandlers;
using Common.Serialization;
using Missions.Agents.Messages;
using System;
using Xunit;

namespace E2E.Tests.Services.Missions;

/// <summary>
/// Regression coverage for the player-order voice serialization boundary.
/// </summary>
public class NetworkAgentVoicePlayedSerializationTests
{
    [Fact]
    public void NetworkAgentVoicePlayed_RoundTripsAgentVoiceTypeAndSample()
    {
        var original = new NetworkAgentVoicePlayed(Guid.NewGuid(), "Charge", "rick_charge_03");
        var serializer = new ProtoBufSerializer(new SerializableTypeMapper());
        MessagePacket packet = MessagePacket.Create(original, serializer);

        var result = Assert.IsType<NetworkAgentVoicePlayed>(serializer.Deserialize<IMessage>(packet.Data));

        Assert.Equal(original.AgentId, result.AgentId);
        Assert.Equal("Charge", result.VoiceTypeId);
        Assert.Equal("rick_charge_03", result.SampleName);
    }

    [Fact]
    public void NetworkAgentVoicePlayed_RoundTripsVanillaFallback()
    {
        var original = new NetworkAgentVoicePlayed(Guid.NewGuid(), "AttackGate", null);
        var serializer = new ProtoBufSerializer(new SerializableTypeMapper());
        MessagePacket packet = MessagePacket.Create(original, serializer);

        var result = Assert.IsType<NetworkAgentVoicePlayed>(serializer.Deserialize<IMessage>(packet.Data));

        Assert.Equal(original.AgentId, result.AgentId);
        Assert.Equal("AttackGate", result.VoiceTypeId);
        Assert.Null(result.SampleName);
    }
}
