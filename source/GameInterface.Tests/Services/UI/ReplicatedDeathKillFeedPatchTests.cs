using System;
using Common.Util;
using GameInterface.Services.MapEvents;
using GameInterface.Services.UI.Patches;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace GameInterface.Tests.Services.UI;

/// <summary>Regression coverage for replicated death kill-feed context.</summary>
public class ReplicatedDeathKillFeedPatchTests : IDisposable
{
    public void Dispose()
    {
        BattleSpawnGate.EndBattle();
    }

    [Fact]
    public void Prefix_RestoresAffectorAndKillingBlow_OnlyInsideMatchingDeathScope()
    {
        BattleSpawnGate.BeginBattle("battle-1");
        var affectedAgent = ObjectHelper.SkipConstructor<Agent>();
        var otherAgent = ObjectHelper.SkipConstructor<Agent>();
        var affectorAgent = ObjectHelper.SkipConstructor<Agent>();
        var blow = new Blow(-1)
        {
            InflictedDamage = 91,
            DamageType = DamageTypes.Cut,
            VictimBodyPart = BoneBodyPartType.Head,
        };
        var expectedKillingBlow = new KillingBlow(blow, Vec3.Zero, Vec3.Zero, 123, 0);

        BattleSpawnGate.RunWithReplicatedDeath(affectedAgent, affectorAgent, expectedKillingBlow, () =>
        {
            Agent actualAffector = null!;
            KillingBlow actualKillingBlow = default;

            ReplicatedDeathKillFeedPatch.Prefix(affectedAgent, ref actualAffector, ref actualKillingBlow);

            Assert.Same(affectorAgent, actualAffector);
            Assert.Equal(123, actualKillingBlow.DeathAction);
            Assert.Equal(91, actualKillingBlow.InflictedDamage);

            actualAffector = null!;
            actualKillingBlow = default;
            ReplicatedDeathKillFeedPatch.Prefix(otherAgent, ref actualAffector, ref actualKillingBlow);
            Assert.Null(actualAffector);
            Assert.False(actualKillingBlow.IsValid);
        });

        Assert.False(BattleSpawnGate.TryGetReplicatedDeath(affectedAgent, out _, out _));
    }
}
