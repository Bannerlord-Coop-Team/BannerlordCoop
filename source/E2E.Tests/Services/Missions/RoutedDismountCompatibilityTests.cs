using Missions.Battles;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace E2E.Tests.Services.Missions;

public class RoutedDismountCompatibilityTests
{
    [Fact]
    public void StaleDismountReaction_IsRemovedWithoutChangingOtherFlags()
    {
        var blow = new Blow
        {
            BlowFlag = BlowFlags.CanDismount | BlowFlags.NonTipThrust,
        };

        bool removed = BattleDamageRouter.RemoveIncompatibleDismountFlag(
            ref blow,
            hasNativeMountedPair: false);

        Assert.True(removed);
        Assert.Equal(BlowFlags.NonTipThrust, blow.BlowFlag);
    }

    [Fact]
    public void CurrentMountedPair_PreservesDismountReaction()
    {
        var blow = new Blow
        {
            BlowFlag = BlowFlags.CanDismount | BlowFlags.NonTipThrust,
        };

        bool removed = BattleDamageRouter.RemoveIncompatibleDismountFlag(
            ref blow,
            hasNativeMountedPair: true);

        Assert.False(removed);
        Assert.Equal(
            BlowFlags.CanDismount | BlowFlags.NonTipThrust,
            blow.BlowFlag);
    }

    [Fact]
    public void OrdinaryReaction_IsUnchangedWithoutMountedPair()
    {
        var blow = new Blow
        {
            BlowFlag = BlowFlags.KnockBack,
        };

        bool removed = BattleDamageRouter.RemoveIncompatibleDismountFlag(
            ref blow,
            hasNativeMountedPair: false);

        Assert.False(removed);
        Assert.Equal(BlowFlags.KnockBack, blow.BlowFlag);
    }
}
