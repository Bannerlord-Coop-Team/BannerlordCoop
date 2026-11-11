using E2E.Tests.Environment.MockEngine;
using Missions.Battles;
using Missions.Messages;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions.Naval;

/// <summary>
/// M12: ship siege weapons follow their hull's owner. Only the owner simulates a hull's weapon; peers turn, reload and fire
/// the same weapon on their copy from the owner's state and shots, and a replayed projectile never deals damage, so a hit
/// applies once, on the struck hull's owner.
/// </summary>
public class NavalShipWeaponScenarioTests : NavalBattleTestEnvironment
{
    private const string Ballista = "ballista_projectile";

    public NavalShipWeaponScenarioTests(ITestOutputHelper output) : base(output, numClients: 2) { }

    // B's ballista fires at the host's AI hull. The host plays the bolt on its copy of B's hull with B's gunner puppet, whose
    // hits the damage gates drop, and B's own bolt hits the AI hull once, on the host.
    [Fact]
    public void PlayerBallistaShot_ShowsOnTheHost_AndItsHitAppliesOnceOnTheHullOwner()
    {
        using var battle = StartServerBattle(atSea: true, out var a, out var b, out _);
        var hullB = battle.FieldPlayerHull(b, new Vec3(40f, 0f, 0f));
        var pirate = battle.FieldAiHull(a, BattleSideEnum.Defender, new Vec3(40f, 300f, 0f));
        battle.CommitAll();
        battle.TickAll(2);
        ArmEveryHull(battle, hullB, pirate);
        var gunnerId = Guid.NewGuid();
        var gunner = b.RegisterAgent(gunnerId, "B");
        var gunnerPuppet = a.RegisterAgent(gunnerId, "B");
        battle.TickAll(1);

        Assert.Equal(new MissionObject[] { b.Hull(hullB).Weapon! }, b.Engine.SimulatedWeapons);
        Assert.Equal(new MissionObject[] { a.Hull(pirate).Weapon! }, a.Engine.SimulatedWeapons);

        b.FireWeapon(hullB, gunner, new Vec3(40f, 5f, 4f), new Vec3(0f, 1f, 0f), Ballista);
        battle.TickAll(1);

        var replayed = Assert.Single(a.Hull(hullB).ReplayedShots);
        Assert.Same(gunnerPuppet, replayed.Shooter);
        Assert.True(replayed.LaunchEffects);
        Assert.Equal(new Vec3(40f, 5f, 4f), replayed.Shot.Position);
        Assert.Equal(Ballista, replayed.Shot.MissileItemId);
        Assert.Empty(b.Hull(hullB).ReplayedShots);
        bool shooterDrivenHere = true;
        a.Call(() => shooterDrivenHere = a.Agents.IsLocallyControlled(replayed.Shooter));
        Assert.False(shooterDrivenHere);

        b.Hit(pirate, 30f);
        battle.TickAll(2);

        Assert.Single(a.Hull(pirate).AppliedDamage);
        Assert.Empty(b.Hull(pirate).AppliedDamage);
        Assert.Equal(FakeNavalShipEngine.FullHitPoints - 30f, a.Hull(pirate).HitPoints);
        Assert.Equal(FakeNavalShipEngine.FullHitPoints - 30f, b.Hull(pirate).HitPoints);
    }

    // The host's AI ballista aims, reloads and fires at B's hull. B's copy turns and reloads from the host's state, B sees
    // the bolt, and the host's hit applies once, on B.
    [Fact]
    public void AiBallista_AimsReloadsAndFiresOnThePlayersCopy_AndItsHitAppliesOnceOnThePlayer()
    {
        using var battle = StartServerBattle(atSea: true, out var a, out var b, out _);
        var hullB = battle.FieldPlayerHull(b, new Vec3(40f, 0f, 0f));
        var pirate = battle.FieldAiHull(a, BattleSideEnum.Defender, new Vec3(40f, 300f, 0f));
        battle.CommitAll();
        battle.TickAll(2);
        ArmEveryHull(battle, hullB, pirate);
        var gunnerId = Guid.NewGuid();
        var gunner = a.RegisterAgent(gunnerId, "A");
        var gunnerPuppet = b.RegisterAgent(gunnerId, "A");
        battle.TickAll(3);
        int appliedBefore = b.Hull(pirate).AppliedWeaponStates;

        a.Hull(pirate).WeaponState = new BattleShipWeaponState(0.6f, 0.2f, (int)RangedSiegeWeapon.WeaponState.Reloading, 3);
        battle.TickAll(3);

        var copyState = b.Hull(pirate).WeaponState;
        Assert.Equal(appliedBefore + 1, b.Hull(pirate).AppliedWeaponStates);
        Assert.Equal(0.6f, copyState.Direction);
        Assert.Equal(0.2f, copyState.ReleaseAngle);
        Assert.Equal((int)RangedSiegeWeapon.WeaponState.Reloading, copyState.WeaponState);
        Assert.Equal(3, copyState.AmmoCount);
        Assert.Equal(0, a.Hull(pirate).AppliedWeaponStates);

        a.FireWeapon(pirate, gunner, new Vec3(40f, 295f, 4f), new Vec3(0f, -1f, 0f), Ballista);
        battle.TickAll(1);

        Assert.Same(gunnerPuppet, Assert.Single(b.Hull(pirate).ReplayedShots).Shooter);
        Assert.Empty(a.Hull(pirate).ReplayedShots);

        a.Hit(hullB, 20f);
        battle.TickAll(2);

        Assert.Single(b.Hull(hullB).AppliedDamage);
        Assert.Empty(a.Hull(hullB).AppliedDamage);
        Assert.Equal(FakeNavalShipEngine.FullHitPoints - 20f, a.Hull(hullB).HitPoints);
    }

    // A copy whose hull path does not match the owner's weapon shows no shot, rather than firing some other weapon.
    [Fact]
    public void ShotForAWeaponTheCopyDoesNotCarry_IsNotReplayed()
    {
        using var battle = StartServerBattle(atSea: true, out var a, out var b, out _);
        var hullB = battle.FieldPlayerHull(b, new Vec3(40f, 0f, 0f));
        battle.CommitAll();
        battle.TickAll(2);
        b.Hull(hullB).ArmWeapon();
        a.Hull(hullB).ArmWeapon("3:mangonel_holder/0:ship_mangonel");
        battle.TickAll(1);

        b.FireWeapon(hullB, null, new Vec3(40f, 5f, 4f), new Vec3(0f, 1f, 0f), Ballista);
        battle.TickAll(1);

        var stats = a.WeaponStats();
        Assert.Empty(a.Hull(hullB).ReplayedShots);
        Assert.Equal(1, (long)stats["shotsDropped"]!);
        Assert.Equal(0, (long)stats["statesApplied"]!);
    }

    private static void ArmEveryHull(NavalBattle battle, params Guid[] ships)
    {
        foreach (var peer in battle.Peers)
        {
            foreach (var shipId in ships)
                peer.Hull(shipId).ArmWeapon();
        }
    }
}
