using Autofac;
using GameInterface.Serialization;
using GameInterface.Surrogates;
using GameInterface.Tests.Bootstrap.Modules;
using ProtoBuf;
using System.IO;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace GameInterface.Tests.Serialization.SerializerTests;

/// <summary>Regression coverage for <see cref="KillingBlow"/> protobuf serialization.</summary>
public class KillingBlowSerializationTest
{
    [Fact]
    public void KillingBlow_RoundTripsThroughProtoSurrogate()
    {
        using var container = BuildContainer();
        var hadPrevious = ContainerProvider.TryGetContainer(out var previousContainer);
        ContainerProvider.SetContainer(container);

        try
        {
            new SurrogateCollection();

            var blow = new Blow(42)
            {
                InflictedDamage = 87,
                DamageType = DamageTypes.Pierce,
                VictimBodyPart = BoneBodyPartType.Head,
            };
            var original = new KillingBlow(blow, Vec3.Zero, new Vec3(1f, 2f, 3f), 321, 0);

            using var stream = new MemoryStream();
            Serializer.Serialize(stream, original);
            stream.Position = 0;

            var result = Serializer.Deserialize<KillingBlow>(stream);

            Assert.True(result.IsValid);
            Assert.Equal(42, result.OwnerId);
            Assert.Equal(87, result.InflictedDamage);
            Assert.Equal(DamageTypes.Pierce, result.DamageType);
            Assert.Equal(BoneBodyPartType.Head, result.VictimBodyPart);
            Assert.Equal(321, result.DeathAction);
        }
        finally
        {
            if (hadPrevious)
                ContainerProvider.SetContainer(previousContainer);
            else
                ContainerProvider.Clear();
        }
    }

    private static IContainer BuildContainer()
    {
        var builder = new ContainerBuilder();
        builder.RegisterModule<SerializationTestModule>();
        return builder.Build();
    }
}
