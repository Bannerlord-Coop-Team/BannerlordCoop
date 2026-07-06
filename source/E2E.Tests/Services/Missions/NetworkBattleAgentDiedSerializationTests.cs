using Autofac;
using GameInterface;
using GameInterface.Serialization;
using GameInterface.Services.ObjectManager;
using GameInterface.Surrogates;
using Missions.Messages;
using ProtoBuf;
using Serilog;
using System;
using System.IO;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace E2E.Tests.Services.Missions;

/// <summary>Regression coverage for death-message serialization.</summary>
public class NetworkBattleAgentDiedSerializationTests
{
    [Fact]
    public void NetworkBattleAgentDied_RoundTripsAffectorAndKillingBlow()
    {
        using var container = BuildContainer();
        var hadPrevious = ContainerProvider.TryGetContainer(out var previousContainer);
        ContainerProvider.SetContainer(container);

        try
        {
            new SurrogateCollection();

            var agentId = Guid.NewGuid();
            var affectorAgentId = Guid.NewGuid();
            var blow = new Blow(17)
            {
                InflictedDamage = 93,
                DamageType = DamageTypes.Cut,
                VictimBodyPart = BoneBodyPartType.Head,
            };
            var original = new NetworkBattleAgentDied(
                agentId,
                wounded: false,
                affectorAgentId,
                new KillingBlow(blow, Vec3.Zero, Vec3.Zero, 456, 0));

            using var stream = new MemoryStream();
            Serializer.Serialize(stream, original);
            stream.Position = 0;

            var result = Serializer.Deserialize<NetworkBattleAgentDied>(stream);

            Assert.Equal(agentId, result.AgentId);
            Assert.False(result.Wounded);
            Assert.Equal(affectorAgentId, result.AffectorAgentId);
            Assert.True(result.KillingBlow.IsValid);
            Assert.Equal(17, result.KillingBlow.OwnerId);
            Assert.Equal(93, result.KillingBlow.InflictedDamage);
            Assert.Equal(DamageTypes.Cut, result.KillingBlow.DamageType);
            Assert.Equal(BoneBodyPartType.Head, result.KillingBlow.VictimBodyPart);
            Assert.Equal(456, result.KillingBlow.DeathAction);
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
        builder.RegisterInstance(new LoggerConfiguration().CreateLogger()).As<ILogger>();
        builder.RegisterType<ObjectManager>().As<IObjectManager>();
        builder.RegisterType<BinaryPackageFactory>().As<IBinaryPackageFactory>();
        return builder.Build();
    }
}
