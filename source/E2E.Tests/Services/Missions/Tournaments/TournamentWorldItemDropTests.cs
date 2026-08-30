using Common.Messaging;
using Common.PacketHandlers;
using Common.Serialization;
using GameInterface.Surrogates;
using Missions.Agents.Messages;
using Missions.Agents.Packets;
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
        const string bannerCode = "1.2.3.1528.1528.764.764.0.0.0";
        var rotation = new Mat3(
            new Vec3(1f, 2f, 3f),
            new Vec3(4f, 5f, 6f),
            new Vec3(7f, 8f, 9f));
        var currentEquipment = new AgentEquipmentData(
            EquipmentIndex.Weapon1,
            EquipmentIndex.Weapon3,
            2);
        var message = new NetworkWeaponDropped(
            worldItemId,
            agentId,
            EquipmentIndex.Weapon0,
            worldItemId,
            "controller-a",
            "sword_test",
            "modifier_test",
            bannerCode,
            17,
            new Vec3(1f, 2f, 3f),
            rotation,
            (int)TaleWorlds.MountAndBlade.Mission.WeaponSpawnFlags.WithPhysics,
            hasLifeTime: true,
            remainingLifeTime: 180f,
            currentEquipment,
            isCatchUp: false);

        var serializer = new ProtoBufSerializer(new SerializableTypeMapper());
        MessagePacket packet = MessagePacket.Create(message, serializer);
        var received = Assert.IsType<NetworkWeaponDropped>(
            serializer.Deserialize<IMessage>(packet.Data));

        Assert.Equal(worldItemId, received.DropId);
        Assert.Equal(agentId, received.AgentId);
        Assert.Equal(worldItemId, received.WorldItemId);
        Assert.Equal("controller-a", received.OriginControllerId);
        Assert.Equal("sword_test", received.ItemObjectId);
        Assert.Equal("modifier_test", received.ItemModifierId);
        Assert.Equal(bannerCode, received.BannerCode);
        Assert.Equal((short)17, received.DataValue);
        Assert.Equal(1f, received.Position.x);
        Assert.Equal(2f, received.Position.y);
        Assert.Equal(3f, received.Position.z);
        Assert.Equal(rotation, received.Rotation);
        Assert.Equal(
            (int)TaleWorlds.MountAndBlade.Mission.WeaponSpawnFlags.WithPhysics,
            received.SpawnFlags);
        Assert.True(received.HasLifeTime);
        Assert.Equal(180f, received.RemainingLifeTime);
        Assert.True(received.HasCurrentEquipment);
        Assert.Equal((int)EquipmentIndex.Weapon1, received.CurrentEquipment.MainHandIndex);
        Assert.Equal((int)EquipmentIndex.Weapon3, received.CurrentEquipment.OffHandIndex);
        Assert.Equal(2, received.CurrentEquipment.MainHandUsageIndex);
        Assert.False(received.IsCatchUp);
    }
}
