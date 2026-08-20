using GameInterface.Surrogates;
using Missions.Agents.Messages;
using ProtoBuf;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace E2E.Tests.Services.Missions.Tournaments;

public class TournamentWorldItemDropTests
{
    [Fact]
    public void ReplicatedWeaponDrop_CarriesRuntimeWorldItemId()
    {
        _ = new SurrogateCollection();

        Guid worldItemId = Guid.NewGuid();
        Guid agentId = Guid.NewGuid();
        var message = new NetworkWeaponDropped(
            worldItemId,
            agentId,
            EquipmentIndex.Weapon0,
            worldItemId,
            "controller-a",
            "sword_test",
            "modifier_test",
            null,
            17,
            new Vec3(1f, 2f, 3f),
            Mat3.Identity,
            (int)TaleWorlds.MountAndBlade.Mission.WeaponSpawnFlags.WithPhysics,
            hasLifeTime: true,
            remainingLifeTime: 180f,
            currentEquipment: null,
            isCatchUp: false);
        using var stream = new MemoryStream();
        Serializer.Serialize(stream, message);
        stream.Position = 0;

        NetworkWeaponDropped received = Serializer.Deserialize<NetworkWeaponDropped>(stream);

        Assert.Equal(worldItemId, received.DropId);
        Assert.Equal(agentId, received.AgentId);
        Assert.Equal(worldItemId, received.WorldItemId);
        Assert.Equal("controller-a", received.OriginControllerId);
        Assert.Equal("sword_test", received.ItemObjectId);
        Assert.Equal("modifier_test", received.ItemModifierId);
        Assert.Equal((short)17, received.DataValue);
        Assert.Equal(1f, received.Position.x);
        Assert.Equal(2f, received.Position.y);
        Assert.Equal(3f, received.Position.z);
        Assert.True(received.HasLifeTime);
        Assert.Equal(180f, received.RemainingLifeTime);
        Assert.False(received.HasCurrentEquipment);
        Assert.False(received.IsCatchUp);
    }
}
