using Common.Messaging;
using Common.PacketHandlers;
using Common.Serialization;
using GameInterface.Surrogates;
using Missions.Battles;
using Missions.Messages;
using Missions.Missiles;
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
    private static readonly MissileOrientationCodec Codec = new MissileOrientationCodec();

    public NetworkAgentShootSerializationTests()
    {
        new SurrogateCollection();
    }

    [Fact]
    public void NetworkAgentShoot_RoundTripsWeaponIdsAndLaunchData()
    {
        Mat3 orientation = Mat3.Identity;
        orientation.RotateAboutUp(0.6f);
        orientation.RotateAboutSide(-0.3f);
        orientation.RotateAboutForward(1.1f);
        Assert.True(Codec.TryPack(orientation, out ulong packed));
        var original = new NetworkAgentShoot(
            Guid.NewGuid(),
            new Vec3(1.25f, 2.5f, 3.75f),
            new Vec3(4.5f, 5.75f, 6.25f),
            packed,
            exactOrientation: null,
            hasRigidBody: true,
            missileItemId: "bodkin_arrows",
            itemModifierId: "balanced",
            banner: null,
            baseSpeed: 55.5f,
            speed: 61.25f,
            currentUsageIndex: 3,
            shotSequence: 4_500_000_123L);

        NetworkAgentShoot result = RoundTrip(original);

        Assert.Equal(original.AgentId, result.AgentId);
        Assert.Equal(original.Position, result.Position);
        Assert.Equal(original.Velocity, result.Velocity);
        Assert.Equal(packed, result.PackedOrientation);
        Assert.Null(result.ExactOrientation);
        MissileShotReplayTests.AssertWithinTolerance(orientation, Codec.Unpack(result.PackedOrientation));
        Assert.True(result.HasRigidBody);
        Assert.Equal("bodkin_arrows", result.MissileItemId);
        Assert.Equal("balanced", result.ItemModifierId);
        Assert.Null(result.Banner);
        Assert.Equal(55.5f, result.BaseSpeed);
        Assert.Equal(61.25f, result.Speed);
        Assert.Equal(3, result.CurrentUsageIndex);
        Assert.Equal(4_500_000_123L, result.ShotSequence);
    }

    [Fact]
    public void NetworkAgentShoot_KeepsAFrameThatIsntARotationExactly()
    {
        var skewed = new Mat3(new Vec3(1f, 2f, 3f), new Vec3(4f, 5f, 6f), new Vec3(7f, 8f, 9f));

        NetworkAgentShoot result = RoundTrip(CreateBodkinShot(banner: null, exactOrientation: skewed));

        Assert.Equal(skewed, result.ExactOrientation);
    }

    [Fact]
    public void NetworkAgentShoot_RoundTripsATableauItemBanner()
    {
        var banner = new Banner(MissileShotReplayTests.ClanBannerCode);

        NetworkAgentShoot result = RoundTrip(CreateBodkinShot(banner));

        Assert.Equal(MissileShotReplayTests.ClanBannerCode, result.Banner.Serialize());
    }

    [Fact]
    public void BodkinArrowShot_DropsTheBannerAndFullMatrixBytes()
    {
        var skewed = new Mat3(new Vec3(1f, 2f, 3f), new Vec3(4f, 5f, 6f), new Vec3(7f, 8f, 9f));
        var banner = new Banner(MissileShotReplayTests.ClanBannerCode);

        int arrowBytes = WireLength(CreateBodkinShot(banner: null));
        int withBannerBytes = WireLength(CreateBodkinShot(banner));
        int withFullMatrixBytes = WireLength(CreateBodkinShot(banner: null, exactOrientation: skewed));

        // Development sent this shot as 246 B: about 102 plus the banner, the full Mat3 and a 2-byte missile index.
        Assert.InRange(arrowBytes, 90, 115);
        Assert.Equal(arrowBytes + 100, withBannerBytes);
        Assert.Equal(arrowBytes + 42, withFullMatrixBytes);
    }

    // A shot carries either the packed launch rotation or, when that doesn't fit, the exact frame.
    private static NetworkAgentShoot CreateBodkinShot(Banner banner, Mat3? exactOrientation = null)
    {
        Mat3 orientation = Mat3.Identity;
        orientation.RotateAboutUp(0.6f);
        orientation.RotateAboutSide(0.25f);
        Assert.True(Codec.TryPack(orientation, out ulong packed));
        return new NetworkAgentShoot(
            Guid.NewGuid(),
            new Vec3(512.37f, 834.21f, 23.65f),
            new Vec3(48.2f, 31.7f, 9.4f),
            exactOrientation == null ? packed : 0UL,
            exactOrientation,
            hasRigidBody: false,
            missileItemId: "bodkin_arrows_a",
            itemModifierId: null,
            banner,
            baseSpeed: 58.4f,
            speed: 59.1f,
            currentUsageIndex: 0,
            shotSequence: 4_500_000_123L);
    }

    private static NetworkAgentShoot RoundTrip(NetworkAgentShoot original)
    {
        var serializer = new ProtoBufSerializer(new SerializableTypeMapper());
        MessagePacket packet = MessagePacket.Create(original, serializer);
        return Assert.IsType<NetworkAgentShoot>(serializer.Deserialize<IMessage>(packet.Data));
    }

    private static int WireLength(NetworkAgentShoot shot) =>
        MessagePacket.Create(shot, new ProtoBufSerializer(new SerializableTypeMapper())).Data.Length;

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
        AttackCollisionData collisionData = default;
        var mapper = new BattleDamageDataMapper();
        BattleDamageData damageData = mapper.Pack(in blow, in collisionData);

        var original = new NetworkApplyBattleDamage(
            Guid.NewGuid(),
            Guid.NewGuid(),
            damageData,
            blow.IsMissile,
            missileShotSequence: 4_500_000_123L,
            attackerWeapon: attackerWeapon,
            sourceControllerId: "collision-peer");

        var serializer = new ProtoBufSerializer(new SerializableTypeMapper());
        MessagePacket packet = MessagePacket.Create(original, serializer);

        var result = Assert.IsType<NetworkApplyBattleDamage>(serializer.Deserialize<IMessage>(packet.Data));

        Assert.True(mapper.TryResolve(result.DamageData, out Blow decodedBlow, out _));
        Assert.InRange(packet.Data.Length, 1, 1199);
        Assert.Equal(blow.InflictedDamage, decodedBlow.InflictedDamage);
        Assert.Equal(blow.WeaponRecord.AffectorWeaponSlotOrMissileIndex,
            decodedBlow.WeaponRecord.AffectorWeaponSlotOrMissileIndex);
        Assert.Equal(original.VictimAgentId, result.VictimAgentId);
        Assert.Equal(original.AttackerAgentId, result.AttackerAgentId);
        Assert.True(result.IsMissile);
        Assert.Equal(4_500_000_123L, result.MissileShotSequence);
        Assert.Equal("collision-peer", result.SourceControllerId);
        Assert.NotNull(result.AttackerWeapon);
        Assert.Equal(WeaponClass.Arrow, result.AttackerWeapon.WeaponClass);
    }
}
