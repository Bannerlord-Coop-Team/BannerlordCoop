using Missions.Messages;
using ProtoBuf;
using System;
using System.IO;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace E2E.Tests.Services.Missions;

/// <summary>Regression coverage for death-message serialization.</summary>
public class NetworkBattleAgentDiedSerializationTests
{
    [Fact]
    public void NetworkBattleAgentDied_RoundTripsAffectorAndDeathMetadata()
    {
        var agentId = Guid.NewGuid();
        var affectorAgentId = Guid.NewGuid();
        var original = new NetworkBattleAgentDied(
            agentId,
            wounded: false,
            affectorAgentId,
            inflictedDamage: 93,
            victimBodyPart: BoneBodyPartType.Head,
            deathAction: 456);

        using var stream = new MemoryStream();
        Serializer.Serialize(stream, original);
        stream.Position = 0;

        var result = Serializer.Deserialize<NetworkBattleAgentDied>(stream);

        Assert.Equal(agentId, result.AgentId);
        Assert.False(result.Wounded);
        Assert.Equal(affectorAgentId, result.AffectorAgentId);
        Assert.Equal(93, result.InflictedDamage);
        Assert.Equal(BoneBodyPartType.Head, result.VictimBodyPart);
        Assert.Equal(456, result.DeathAction);
    }
}
