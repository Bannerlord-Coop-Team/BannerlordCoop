using Common.Messaging;
using Common.PacketHandlers;
using Common.Serialization;
using GameInterface.Surrogates;
using Missions.Data;
using Missions.Messages;
using Missions.Taverns;
using System;
using TaleWorlds.Library;
using Xunit;

namespace E2E.Tests.Services.Missions;

/// <summary>
/// Regression coverage for party-agent join data.
/// </summary>
public class NetworkMissionJoinInfoSerializationTests
{
    public NetworkMissionJoinInfoSerializationTests()
    {
        _ = new SurrogateCollection();
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void RemotePartyAgent_DisablesHorsesOnlyWhenOwnerIsDismounted(
        bool hasMount,
        bool expected)
    {
        Assert.Equal(expected, CoopLocationsController.ShouldDisableHorses(hasMount));
    }

    [Fact]
    public void CompanionMountState_RoundTrips()
    {
        var spawn = new CoopAgentSpawnData(
            Guid.NewGuid(),
            "companion",
            new Vec3(1f, 2f, 3f),
            75f,
            isPlayer: false,
            hasMount: true);
        var original = new NetworkMissionJoinInfo("owner", true, new[] { spawn });
        var serializer = new ProtoBufSerializer(new SerializableTypeMapper());
        MessagePacket packet = MessagePacket.Create(original, serializer);

        var result = Assert.IsType<NetworkMissionJoinInfo>(
            serializer.Deserialize<IMessage>(packet.Data));
        CoopAgentSpawnData companion = Assert.Single(result.AiAgentData);

        Assert.True(companion.HasMount);
        Assert.False(companion.IsPlayer);
    }
}
