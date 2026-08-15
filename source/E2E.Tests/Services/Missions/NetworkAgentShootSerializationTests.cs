using Common.Messaging;
using Common.PacketHandlers;
using Common.Serialization;
using GameInterface.Surrogates;
using Missions.Messages;
using Missions.Missiles.Message;
using System;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace E2E.Tests.Services.Missions;

/// <summary>Regression coverage for the complete projectile-message serialization boundary.</summary>
public class NetworkAgentShootSerializationTests
{
    public NetworkAgentShootSerializationTests()
    {
        new SurrogateCollection();
    }

    [Fact]
    public void NetworkAgentShoot_RoundTripsWeaponIdsAndLaunchData()
    {
        var original = new NetworkAgentShoot(
            Guid.NewGuid(),
            new Vec3(1.25f, 2.5f, 3.75f),
            new Vec3(4.5f, 5.75f, 6.25f),
            new Mat3(
                new Vec3(1f, 2f, 3f),
                new Vec3(4f, 5f, 6f),
                new Vec3(7f, 8f, 9f)),
            hasRigidBody: true,
            missileItemId: "bodkin_arrows",
            itemModifierId: "balanced",
            banner: null,
            missileIndex: 42,
            baseSpeed: 55.5f,
            speed: 61.25f,
            currentUsageIndex: 3,
            shotSequence: 4_500_000_123L);

        var serializer = new ProtoBufSerializer(new SerializableTypeMapper());
        MessagePacket packet = MessagePacket.Create(original, serializer);

        var result = Assert.IsType<NetworkAgentShoot>(serializer.Deserialize<IMessage>(packet.Data));

        Assert.Equal(original.AgentId, result.AgentId);
        Assert.Equal(original.Position, result.Position);
        Assert.Equal(original.Velocity, result.Velocity);
        Assert.Equal(original.Orientation, result.Orientation);
        Assert.True(result.HasRigidBody);
        Assert.Equal("bodkin_arrows", result.MissileItemId);
        Assert.Equal("balanced", result.ItemModifierId);
        Assert.Null(result.Banner);
        Assert.Equal(42, result.MissileIndex);
        Assert.Equal(55.5f, result.BaseSpeed);
        Assert.Equal(61.25f, result.Speed);
        Assert.Equal(3, result.CurrentUsageIndex);
        Assert.Equal(4_500_000_123L, result.ShotSequence);
    }

    [Fact]
    public void NetworkApplyBattleDamage_RoundTripsMissileContext()
    {
        var blow = new Blow(17)
        {
            InflictedDamage = 23,
            DamageType = DamageTypes.Pierce,
        };
        blow.WeaponRecord._isMissile = true;
        blow.WeaponRecord.AffectorWeaponSlotOrMissileIndex = 42;
        var attackerWeapon = new WeaponComponentData(null, WeaponClass.Arrow, default);

        var original = new NetworkApplyBattleDamage(
            Guid.NewGuid(),
            Guid.NewGuid(),
            blow,
            default,
            missileShotSequence: 4_500_000_123L,
            attackerWeapon: attackerWeapon);

        var serializer = new ProtoBufSerializer(new SerializableTypeMapper());
        MessagePacket packet = MessagePacket.Create(original, serializer);

        var result = Assert.IsType<NetworkApplyBattleDamage>(serializer.Deserialize<IMessage>(packet.Data));

        Assert.Equal(original.VictimAgentId, result.VictimAgentId);
        Assert.Equal(original.AttackerAgentId, result.AttackerAgentId);
        Assert.True(result.IsMissile);
        Assert.Equal(4_500_000_123L, result.MissileShotSequence);
        Assert.NotNull(result.AttackerWeapon);
        Assert.Equal(WeaponClass.Arrow, result.AttackerWeapon.WeaponClass);
    }
}
