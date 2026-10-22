using Common.Util;
using GameInterface;
using GameInterface.Services.MapEvents;
using GameInterface.Services.UI.Patches;
using GameInterface.Tests.Services.MapEvents;
using System;
using System.Threading;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace GameInterface.Tests.Services.UI;

/// <summary>Tests that the combat-log patches drive the kill-feed tracker resolved from the container.</summary>
[Collection(ActiveCoopBattleCollection.Name)]
public class RoutedPlayerHitCombatLogPatchTests
{
    // Inside a coop battle the enqueue patch holds the resolved tracker's lock until its finalizer runs.
    [Fact]
    public void AddCombatLogPatch_HoldsResolvedTrackerLockUntilFinalizer()
    {
        using var battleScope = new ActiveCoopBattleScope("battle-1");
        Assert.True(ContainerProvider.TryResolve<IRoutedPlayerHitNotificationTracker>(out var tracker));
        var attackerAgent = ObjectHelper.SkipConstructor<Agent>();

        RoutedPlayerHitCombatLogPatch.Prefix(out var lockScope);
        Assert.NotNull(lockScope);
        var otherThreadEnqueue = new Thread(() => tracker.EnqueueCombatLogContext(null!, 0));
        try
        {
            otherThreadEnqueue.Start();
            Assert.False(otherThreadEnqueue.Join(TimeSpan.FromMilliseconds(100)));
            RoutedPlayerHitCombatLogPatch.Postfix(attackerAgent, null!, default, lockScope);
        }
        finally
        {
            RoutedPlayerHitCombatLogPatch.Finalizer(null!, lockScope);
        }

        Assert.True(otherThreadEnqueue.Join(TimeSpan.FromSeconds(5)));
    }

    // When vanilla's enqueue throws, the finalizer still releases the lock and hands the same exception back.
    [Fact]
    public void AddCombatLogPatch_ReleasesLockAndReturnsException_WhenEnqueueThrows()
    {
        using var battleScope = new ActiveCoopBattleScope("battle-1");
        Assert.True(ContainerProvider.TryResolve<IRoutedPlayerHitNotificationTracker>(out var tracker));
        var thrown = new InvalidOperationException("enqueue failed");

        RoutedPlayerHitCombatLogPatch.Prefix(out var lockScope);
        Assert.NotNull(lockScope);
        var returned = RoutedPlayerHitCombatLogPatch.Finalizer(thrown, lockScope);

        Assert.Same(thrown, returned);
        var otherThreadEnqueue = new Thread(() => tracker.EnqueueCombatLogContext(null!, 0));
        otherThreadEnqueue.Start();
        Assert.True(otherThreadEnqueue.Join(TimeSpan.FromSeconds(5)));
    }

    // Outside a coop battle the enqueue patch resolves nothing, so its postfix and finalizer do nothing.
    [Fact]
    public void AddCombatLogPatch_SkipsTracker_WhenNoCoopBattleIsActive()
    {
        using var battleScope = new ActiveCoopBattleScope(null!);

        RoutedPlayerHitCombatLogPatch.Prefix(out var lockScope);

        Assert.Null(lockScope);
    }

    // The publish patch exposes the queued routed hit through the resolved tracker and clears it afterwards.
    [Fact]
    public void GenerateCombatLogPatch_ExposesQueuedRoutedHitOnlyWhilePublishing()
    {
        using var battleScope = new ActiveCoopBattleScope("battle-1");
        Assert.True(ContainerProvider.TryResolve<IRoutedPlayerHitNotificationTracker>(out var tracker));
        var affectedAgent = ObjectHelper.SkipConstructor<Agent>();
        tracker.EnqueueCombatLogContext(affectedAgent, 50);

        RoutedPlayerHitCombatLogContextPatch.Prefix(out var state);
        Assert.NotNull(state);
        Assert.True(tracker.TryGetCurrentRoutedPlayerHit(out var actualAgent, out int damage));
        Assert.Same(affectedAgent, actualAgent);
        Assert.Equal(50, damage);
        RoutedPlayerHitCombatLogContextPatch.Finalizer(state);

        Assert.False(tracker.TryGetCurrentRoutedPlayerHit(out _, out _));
    }

    // Without a container the publish patch resolves no tracker, so it exposes nothing and its finalizer does nothing.
    [Fact]
    public void GenerateCombatLogPatch_SkipsTracker_WhenResolveFails()
    {
        bool hadPreviousContainer = ContainerProvider.TryGetContainer(out var previousContainer);
        ContainerProvider.Clear();
        try
        {
            RoutedPlayerHitCombatLogContextPatch.Prefix(out var state);

            Assert.Null(state);
            RoutedPlayerHitCombatLogContextPatch.Finalizer(state);
        }
        finally
        {
            if (hadPreviousContainer) ContainerProvider.SetContainer(previousContainer);
        }
    }
}
