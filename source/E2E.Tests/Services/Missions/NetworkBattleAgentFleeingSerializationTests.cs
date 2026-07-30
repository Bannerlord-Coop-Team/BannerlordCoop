using Common.Messaging;
using Common.PacketHandlers;
using Common.Serialization;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Surrogates;
using Missions.Data;
using Missions.Messages;
using System;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace E2E.Tests.Services.Missions;

public class NetworkBattleAgentFleeingSerializationTests
{
    public NetworkBattleAgentFleeingSerializationTests()
    {
        new SurrogateCollection();
    }

    [Fact]
    public void NetworkBattleAgentFleeing_RoundTripsAgentId()
    {
        var agentId = Guid.NewGuid();
        var original = new NetworkBattleAgentFleeing(agentId);

        var serializer = new ProtoBufSerializer(new SerializableTypeMapper());
        MessagePacket packet = MessagePacket.Create(original, serializer);
        var result = Assert.IsType<NetworkBattleAgentFleeing>(
            serializer.Deserialize<IMessage>(packet.Data));

        Assert.Equal(agentId, result.AgentId);
    }

    [Fact]
    public void BattleAgentSpawnData_RoundTripsRunningAwayState()
    {
        var original = new BattleAgentSpawnData(
            Guid.NewGuid(),
            "character",
            default,
            BattleSideEnum.Attacker,
            100f,
            "owner",
            "party",
            1,
            new Equipment(),
            new BodyProperties(),
            new MissionEquipmentData(new()),
            isRunningAway: true);

        var serializer = new ProtoBufSerializer(new SerializableTypeMapper());
        var result = Assert.IsType<BattleAgentSpawnData>(
            serializer.Deserialize(serializer.Serialize(original)));

        Assert.True(result.IsRunningAway);
    }

    [Fact]
    public void NetworkRouteBattleEnemies_RoundTripsMapEventAndRemainingFighters()
    {
        var original = new NetworkRouteBattleEnemies("MapEvent_Created_42", 1);
        var serializer = new ProtoBufSerializer(new SerializableTypeMapper());
        MessagePacket packet = MessagePacket.Create(original, serializer);

        var result = Assert.IsType<NetworkRouteBattleEnemies>(
            serializer.Deserialize<IMessage>(packet.Data));

        Assert.Equal("MapEvent_Created_42", result.MapEventId);
        Assert.Equal(1, result.EnemiesToLeaveFighting);
    }
}
