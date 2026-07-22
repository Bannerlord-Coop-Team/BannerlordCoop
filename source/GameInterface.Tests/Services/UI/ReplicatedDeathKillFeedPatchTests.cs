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
        BattleSpawnGate.BeginBattle("battle-1", 1000);
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

        BattleSpawnGate.RunWithReplicatedDeath(affectedAgent, affectorAgent, expectedKillingBlow, AgentState.Killed, () =>
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

    [Fact]
    public void RemoveRoutedPlayerHitNotification_RemovesOnlyMatchingVictimNotification()
    {
        BattleSpawnGate.BeginBattle("battle-1", 1000);
        var firstAgent = ObjectHelper.SkipConstructor<Agent>();
        var secondAgent = ObjectHelper.SkipConstructor<Agent>();
        bool firstRemoved = false;
        bool secondRemoved = false;

        BattleSpawnGate.TrackRoutedPlayerHitNotification(firstAgent, 50, () => firstRemoved = true);
        BattleSpawnGate.TrackRoutedPlayerHitNotification(secondAgent, 50, () => secondRemoved = true);

        BattleSpawnGate.RemoveRoutedPlayerHitNotification(firstAgent, 50);

        Assert.True(firstRemoved);
        Assert.False(secondRemoved);
    }

    [Fact]
    public void TrackRoutedPlayerHitNotification_RemovesNotification_WhenDeathArrivesFirst()
    {
        BattleSpawnGate.BeginBattle("battle-1", 1000);
        var affectedAgent = ObjectHelper.SkipConstructor<Agent>();
        bool notificationRemoved = false;

        BattleSpawnGate.RemoveRoutedPlayerHitNotification(affectedAgent, 50);

        BattleSpawnGate.TrackRoutedPlayerHitNotification(affectedAgent, 50, () => notificationRemoved = true);

        Assert.True(notificationRemoved);
    }

    [Fact]
    public void CombatLogContext_PreservesUntrackedEntryBeforeRoutedHit()
    {
        BattleSpawnGate.BeginBattle("battle-1", 1000);
        var affectedAgent = ObjectHelper.SkipConstructor<Agent>();

        BattleSpawnGate.EnqueueCombatLogContext(null!, 50);
        BattleSpawnGate.EnqueueCombatLogContext(affectedAgent, 50);

        BattleSpawnGate.BeginCombatLog();
        Assert.False(BattleSpawnGate.TryGetCurrentRoutedPlayerHit(out _, out _));
        BattleSpawnGate.EndCombatLog();

        BattleSpawnGate.BeginCombatLog();
        Assert.True(BattleSpawnGate.TryGetCurrentRoutedPlayerHit(out var actualAgent, out int damage));
        Assert.Same(affectedAgent, actualAgent);
        Assert.Equal(50, damage);
        BattleSpawnGate.EndCombatLog();
    }
}
