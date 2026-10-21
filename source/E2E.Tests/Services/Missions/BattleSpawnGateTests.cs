using GameInterface.Services.MapEvents;
using TaleWorlds.Core;

namespace E2E.Tests.Services.Missions;

/// <summary>Unit tests for the routed-blow context <see cref="BattleSpawnGate"/> exposes to native damage patches.</summary>
public class BattleSpawnGateTests : IDisposable
{
    public void Dispose() => BattleSpawnGate.ResetCombatFeedback();

    [Fact]
    public void RoutedBlow_RestoresWeaponAndSourceAfterNestedFailure()
    {
        var outerWeapon = new WeaponComponentData(null, WeaponClass.Arrow, default);
        var innerWeapon = new WeaponComponentData(null, WeaponClass.Bolt, default);

        Assert.Null(BattleSpawnGate.RoutedAttackerWeapon);
        Assert.Null(BattleSpawnGate.RoutedBlowSourceControllerId);

        BattleSpawnGate.RunWithRoutedBlow(outerWeapon, "outer", () =>
        {
            Assert.Same(outerWeapon, BattleSpawnGate.RoutedAttackerWeapon);
            Assert.Equal("outer", BattleSpawnGate.RoutedBlowSourceControllerId);
            Assert.Throws<InvalidOperationException>(() =>
                BattleSpawnGate.RunWithRoutedBlow(innerWeapon, "inner", () =>
                {
                    Assert.Same(innerWeapon, BattleSpawnGate.RoutedAttackerWeapon);
                    Assert.Equal("inner", BattleSpawnGate.RoutedBlowSourceControllerId);
                    throw new InvalidOperationException();
                }));
            Assert.Same(outerWeapon, BattleSpawnGate.RoutedAttackerWeapon);
            Assert.Equal("outer", BattleSpawnGate.RoutedBlowSourceControllerId);
        });

        Assert.Null(BattleSpawnGate.RoutedAttackerWeapon);
        Assert.Null(BattleSpawnGate.RoutedBlowSourceControllerId);
    }
}
