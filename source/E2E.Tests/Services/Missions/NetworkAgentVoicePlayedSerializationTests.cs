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
    public void NetworkAgentVoicePlayed_RoundTripsAgentAndVoiceType()
    {
        var original = new NetworkAgentVoicePlayed(Guid.NewGuid(), "Follow");
        var serializer = new ProtoBufSerializer(new SerializableTypeMapper());
        MessagePacket packet = MessagePacket.Create(original, serializer);

        var result = Assert.IsType<NetworkAgentVoicePlayed>(serializer.Deserialize<IMessage>(packet.Data));

        Assert.Equal(original.AgentId, result.AgentId);
        Assert.Equal("Follow", result.VoiceTypeId);
    }
}
