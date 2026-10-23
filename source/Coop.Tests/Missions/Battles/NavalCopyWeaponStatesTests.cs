#if DEBUG
using HarmonyLib;
using Missions.Messages;
using Missions.Naval;
using Moq;
using System.Collections.Generic;
using TaleWorlds.Engine;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace Coop.Tests.Missions.Battles;

/// <summary>R48 bug 1: a copy weapon's replicated state, kept for the ammo a migration swap carries.</summary>
public class NavalCopyWeaponStatesTests
{
    // A swap takes the state once; the replaced weapon's entry goes with it.
    [Fact]
    public void Take_ReturnsTheRememberedStateOnce()
    {
        var states = new NavalCopyWeaponStates();
        var weapon = ShipTestHulls.CreateWeapon();
        var state = State(RangedSiegeWeapon.WeaponState.LoadingAmmo, 29);

        states.Remember(weapon, state);

        Assert.Same(state, states.Take(weapon));
        Assert.Null(states.Take(weapon));
    }

    // A weapon this client simulates again, or every weapon at battle end, has no replicated state left.
    [Fact]
    public void ForgetAndClear_DropTheStates()
    {
        var states = new NavalCopyWeaponStates();
        var simulated = ShipTestHulls.CreateWeapon();
        var copy = ShipTestHulls.CreateWeapon();
        states.Remember(simulated, State(RangedSiegeWeapon.WeaponState.Idle, 20));
        states.Remember(copy, State(RangedSiegeWeapon.WeaponState.Idle, 20));

        states.Forget(new[] { simulated });

        Assert.Null(states.Take(simulated));
        states.Clear();
        Assert.Null(states.Take(copy));
    }

    // The engine stores each state it applies to a copy's weapon.
    [Fact]
    public void ApplyWeaponState_RemembersTheAuthoritysState()
    {
        var store = new Mock<INavalCopyWeaponStates>();
        var engine = new NavalShipEngine(null!, null!, null!, store.Object);
        var weapon = ShipTestHulls.CreateWeapon();
        AccessTools.Field(typeof(ScriptComponentBehavior), "_gameEntity").SetValue(weapon, NavalShipEngineTests.ValidEntity());
        var state = State(RangedSiegeWeapon.WeaponState.LoadingAmmo, 0);

        engine.ApplyWeaponState(weapon, state, State(RangedSiegeWeapon.WeaponState.LoadingAmmo, 0));

        store.Verify(s => s.Remember(weapon, state), Times.Once);
    }

    // Weapons this client starts simulating drop their replicated state, and the battle end drops every one.
    [Fact]
    public void SimulatedWeapons_ForgetTheirStates_AndClearingForgetsAll()
    {
        var store = new Mock<INavalCopyWeaponStates>();
        var engine = new NavalShipEngine(null!, null!, null!, store.Object);
        var own = new List<MissionObject> { ShipTestHulls.CreateWeapon() };

        engine.SetSimulatedWeapons(own);
        engine.ClearSimulatedWeapons();

        store.Verify(s => s.Forget(own), Times.Once);
        store.Verify(s => s.Clear(), Times.Once);
    }

    private static BattleShipWeaponState State(RangedSiegeWeapon.WeaponState state, int ammo) =>
        new BattleShipWeaponState(0f, 0f, (int)state, ammo);
}
#endif
