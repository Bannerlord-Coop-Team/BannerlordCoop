using Missions.Battles;
using Xunit;

namespace Coop.Tests.Missions.Battles;

public class BattleShipDamageRoutesTests
{
    [Theory]
    [InlineData(true, false, BattleShipHitOrigin.LocalHit, BattleShipDamageRoute.Vanilla)]
    [InlineData(true, false, BattleShipHitOrigin.SelfTick, BattleShipDamageRoute.Vanilla)]
    [InlineData(true, false, BattleShipHitOrigin.ReplayedHit, BattleShipDamageRoute.Drop)]
    [InlineData(false, false, BattleShipHitOrigin.LocalHit, BattleShipDamageRoute.Send)]
    [InlineData(false, false, BattleShipHitOrigin.SelfTick, BattleShipDamageRoute.Drop)]
    [InlineData(false, false, BattleShipHitOrigin.ReplayedHit, BattleShipDamageRoute.Drop)]
    [InlineData(true, true, BattleShipHitOrigin.ReplayedHit, BattleShipDamageRoute.Vanilla)]
    [InlineData(false, true, BattleShipHitOrigin.SelfTick, BattleShipDamageRoute.Vanilla)]
    public void Route_RunsVanillaOnOwnHullsAndRoutedAppliesAndSendsOnlyLocalHitsOnForeignHulls(
        bool isOwnHull, bool isRoutedApply, BattleShipHitOrigin origin, BattleShipDamageRoute expected)
    {
        Assert.Equal(expected, BattleShipDamageRoutes.Route(isOwnHull, isRoutedApply, origin));
    }

    [Theory]
    [InlineData(false, false, BattleShipHitOrigin.LocalHit)]
    [InlineData(true, false, BattleShipHitOrigin.LocalHit)]
    [InlineData(true, true, BattleShipHitOrigin.ReplayedHit)]
    public void AttackOrigin_TreatsOnlyAPuppetsAttackAsAReplay(bool hasAttacker, bool attackerIsPuppet, BattleShipHitOrigin expected)
    {
        Assert.Equal(expected, BattleShipDamageRoutes.AttackOrigin(hasAttacker, attackerIsPuppet));
    }

    [Theory]
    [InlineData(true, false, BattleShipHitOrigin.LocalHit)]
    [InlineData(false, true, BattleShipHitOrigin.LocalHit)]
    [InlineData(false, false, BattleShipHitOrigin.ReplayedHit)]
    public void CollisionOrigin_BelongsToTheRammersOwner(bool isQueuedContact, bool hitterIsOwn, BattleShipHitOrigin expected)
    {
        Assert.Equal(expected, BattleShipDamageRoutes.CollisionOrigin(isQueuedContact, hitterIsOwn));
    }
}
