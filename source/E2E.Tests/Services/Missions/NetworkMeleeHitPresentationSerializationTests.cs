using Common.Messaging;
using Common.PacketHandlers;
using Common.Serialization;
using GameInterface.Surrogates;
using Missions.Agents.Messages;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace E2E.Tests.Services.Missions;

/// <summary>Wire round-trip for <see cref="NetworkMeleeHitPresentation"/>.</summary>
public class NetworkMeleeHitPresentationSerializationTests
{
    public NetworkMeleeHitPresentationSerializationTests()
    {
        _ = new SurrogateCollection();
    }

    [Theory]
    [InlineData(MeleeHitPresentationKind.Blood)]
    [InlineData(MeleeHitPresentationKind.ShieldImpact)]
    [InlineData(MeleeHitPresentationKind.BodyImpact)]
    public void NetworkPresentation_RoundTripsExistingFieldsAndSelectedSound(MeleeHitPresentationKind kind)
    {
        var original = new NetworkMeleeHitPresentation(Guid.NewGuid(), true, kind, 4,
            new Vec3(1f, 2f, 3f), WeaponClass.OneHandedSword, 5, 0.5f, 123, 0.75f);
        var serializer = new ProtoBufSerializer(new SerializableTypeMapper());
        var packet = MessagePacket.Create(original, serializer);

        var result = Assert.IsType<NetworkMeleeHitPresentation>(serializer.Deserialize<IMessage>(packet.Data));

        Assert.Equal(original.VictimAgentId, result.VictimAgentId);
        Assert.True(result.IsMount);
        Assert.Equal(kind, result.Kind);
        Assert.Equal(original.CollisionBoneIndex, result.CollisionBoneIndex);
        Assert.Equal(original.CollisionPosition, result.CollisionPosition);
        Assert.Equal(original.AttackerWeaponClass, result.AttackerWeaponClass);
        Assert.Equal(original.PhysicsMaterialIndex, result.PhysicsMaterialIndex);
        Assert.Equal(original.Strength, result.Strength);
        Assert.Equal(original.SoundIndex, result.SoundIndex);
        Assert.Equal(original.ArmorType, result.ArmorType);
    }
}
