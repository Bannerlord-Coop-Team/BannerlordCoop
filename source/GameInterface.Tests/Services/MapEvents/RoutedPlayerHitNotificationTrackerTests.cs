using Common.Util;
using GameInterface.Services.MapEvents;
using System.Threading;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace GameInterface.Tests.Services.MapEvents;

/// <summary>Tests that <see cref="RoutedPlayerHitNotificationTracker"/> pairs routed hit notifications with replicated deaths.</summary>
public class RoutedPlayerHitNotificationTrackerTests
{
    // A replicated death must remove only the hit notification of its own victim.
    [Fact]
    public void RemoveRoutedPlayerHitNotification_RemovesOnlyMatchingVictimNotification()
    {
        var tracker = new RoutedPlayerHitNotificationTracker();
        var firstAgent = ObjectHelper.SkipConstructor<Agent>();
        var secondAgent = ObjectHelper.SkipConstructor<Agent>();
        bool firstRemoved = false;
        bool secondRemoved = false;

        tracker.TrackRoutedPlayerHitNotification(firstAgent, 50, () => firstRemoved = true);
        tracker.TrackRoutedPlayerHitNotification(secondAgent, 50, () => secondRemoved = true);

        tracker.RemoveRoutedPlayerHitNotification(firstAgent, 50);

        Assert.True(firstRemoved);
        Assert.False(secondRemoved);
    }

    // A hit notification shown after the victim's replicated death must be removed at once.
    [Fact]
    public void TrackRoutedPlayerHitNotification_RemovesNotification_WhenDeathArrivesFirst()
    {
        var tracker = new RoutedPlayerHitNotificationTracker();
        var affectedAgent = ObjectHelper.SkipConstructor<Agent>();
        bool notificationRemoved = false;

        tracker.RemoveRoutedPlayerHitNotification(affectedAgent, 50);

        tracker.TrackRoutedPlayerHitNotification(affectedAgent, 50, () => notificationRemoved = true);

        Assert.True(notificationRemoved);
    }

    // An untracked combat log queued first must not expose the routed hit queued after it.
    [Fact]
    public void CombatLogContext_PreservesUntrackedEntryBeforeRoutedHit()
    {
        var tracker = new RoutedPlayerHitNotificationTracker();
        var affectedAgent = ObjectHelper.SkipConstructor<Agent>();

        tracker.EnqueueCombatLogContext(null!, 50);
        tracker.EnqueueCombatLogContext(affectedAgent, 50);

        using (tracker.BeginCombatLog())
        {
            Assert.False(tracker.TryGetCurrentRoutedPlayerHit(out _, out _));
        }

        using (tracker.BeginCombatLog())
        {
            Assert.True(tracker.TryGetCurrentRoutedPlayerHit(out var actualAgent, out int damage));
            Assert.Same(affectedAgent, actualAgent);
            Assert.Equal(50, damage);
        }

        Assert.False(tracker.TryGetCurrentRoutedPlayerHit(out _, out _));
    }

    // The routed hit is only exposed on the thread publishing the combat log.
    [Fact]
    public void BeginCombatLog_ExposesRoutedHitOnlyOnCallingThread()
    {
        var tracker = new RoutedPlayerHitNotificationTracker();
        var affectedAgent = ObjectHelper.SkipConstructor<Agent>();
        tracker.EnqueueCombatLogContext(affectedAgent, 50);

        using (tracker.BeginCombatLog())
        {
            bool visibleOnOtherThread = true;
            var otherThread = new Thread(() => visibleOnOtherThread = tracker.TryGetCurrentRoutedPlayerHit(out _, out _));
            otherThread.Start();
            otherThread.Join();

            Assert.True(tracker.TryGetCurrentRoutedPlayerHit(out _, out _));
            Assert.False(visibleOnOtherThread);
        }
    }

    // Reset must drop queued combat logs and pending notifications from an earlier battle.
    [Fact]
    public void Reset_DropsEarlierBattleBookkeeping()
    {
        var tracker = new RoutedPlayerHitNotificationTracker();
        var affectedAgent = ObjectHelper.SkipConstructor<Agent>();
        bool notificationRemoved = false;
        tracker.EnqueueCombatLogContext(affectedAgent, 50);
        tracker.TrackRoutedPlayerHitNotification(affectedAgent, 50, () => notificationRemoved = true);

        tracker.Reset();

        using (tracker.BeginCombatLog())
        {
            Assert.False(tracker.TryGetCurrentRoutedPlayerHit(out _, out _));
        }

        tracker.RemoveRoutedPlayerHitNotification(affectedAgent, 50);
        Assert.False(notificationRemoved);
    }
}
