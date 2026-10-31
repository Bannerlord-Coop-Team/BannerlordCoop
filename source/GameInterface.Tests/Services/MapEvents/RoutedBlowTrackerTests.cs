using Common.Util;
using GameInterface.Services.MapEvents;
using System;
using TaleWorlds.Core;
using Xunit;

namespace GameInterface.Tests.Services.MapEvents;

/// <summary>Tests that <see cref="RoutedBlowTracker"/> exposes the routed blow only inside its scope.</summary>
public class RoutedBlowTrackerTests
{
    // An inner routed blow that throws must put the outer blow's weapon and source back.
    [Fact]
    public void BeginRoutedBlow_RestoresWeaponAndSourceAfterNestedFailure()
    {
        var tracker = new RoutedBlowTracker();
        var outerWeapon = ObjectHelper.SkipConstructor<WeaponComponentData>();
        var innerWeapon = ObjectHelper.SkipConstructor<WeaponComponentData>();

        Assert.Null(tracker.AttackerWeapon);
        Assert.Null(tracker.SourceControllerId);

        using (tracker.BeginRoutedBlow(outerWeapon, "outer"))
        {
            Assert.Same(outerWeapon, tracker.AttackerWeapon);
            Assert.Equal("outer", tracker.SourceControllerId);
            Assert.Throws<InvalidOperationException>(ApplyFailingInnerBlow);
            Assert.Same(outerWeapon, tracker.AttackerWeapon);
            Assert.Equal("outer", tracker.SourceControllerId);
        }

        Assert.Null(tracker.AttackerWeapon);
        Assert.Null(tracker.SourceControllerId);

        // Opens the inner blow, checks it is exposed, then fails inside it.
        void ApplyFailingInnerBlow()
        {
            using (tracker.BeginRoutedBlow(innerWeapon, "inner"))
            {
                Assert.Same(innerWeapon, tracker.AttackerWeapon);
                Assert.Equal("inner", tracker.SourceControllerId);
                throw new InvalidOperationException();
            }
        }
    }

    // Disposing a scope twice must not clobber a scope opened after it.
    [Fact]
    public void BeginRoutedBlow_SecondDisposeDoesNothing()
    {
        var tracker = new RoutedBlowTracker();
        var weapon = ObjectHelper.SkipConstructor<WeaponComponentData>();

        var first = tracker.BeginRoutedBlow(weapon, "first");
        first.Dispose();
        using (tracker.BeginRoutedBlow(weapon, "second"))
        {
            first.Dispose();

            Assert.Equal("second", tracker.SourceControllerId);
        }
    }
}
