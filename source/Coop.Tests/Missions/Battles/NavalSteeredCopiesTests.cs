#if DEBUG
using HarmonyLib;
using Missions.Messages;
using Missions.Naval;
using NavalDLC.Missions.Objects;
using System;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using Xunit;
using NavalPhysicsScript = NavalDLC.Missions.NavalPhysics.NavalPhysics;

namespace Coop.Tests.Missions.Battles;

/// <summary>R45 roll: a tracked copy's owner crew load, from Track through Observe to Forget, as the crew load patch finds it.</summary>
public class NavalSteeredCopiesTests
{
    [Fact]
    public void CrewLoad_FollowsTheCopyFromTrackToForget()
    {
        var physics = ShipTestHulls.Uninitialized<NavalPhysicsScript>();
        var ship = ShipTestHulls.Uninitialized<MissionShip>();
        ship._physics = physics;
        AccessTools.Field(typeof(ScriptComponentBehavior), "_gameEntity").SetValue(ship, NavalShipEngineTests.ValidEntity());
        var copies = new NavalSteeredCopies();
        var load = BattleShipCrewLoad.Create(480f, new Vec3(0.5f, -4f, 1.5f));

        try
        {
            copies.Track(ship);
            Assert.True(NavalForeignHulls.TryGetCopy(physics, out var copy));
            Assert.Null(copy.CrewLoad);

            Observe(copies, ship, load);
            Assert.Same(load, copy.CrewLoad);

            Observe(copies, ship, BattleShipCrewLoad.Empty);
            Assert.Same(BattleShipCrewLoad.Empty, copy.CrewLoad);

            // A sample without a load keeps the last one.
            Observe(copies, ship, null);
            Assert.Same(BattleShipCrewLoad.Empty, copy.CrewLoad);

            copies.Forget(ship);
            Assert.False(NavalForeignHulls.TryGetCopy(physics, out _));
        }
        finally
        {
            copies.Forget(ship);
        }
    }

    // Observe stores the load before its native body check, which has no engine behind the fake entity here.
    private static void Observe(NavalSteeredCopies copies, MissionShip ship, BattleShipCrewLoad load) =>
        Assert.ThrowsAny<Exception>(() => copies.Observe(ship, MatrixFrame.Identity, new BattleShipMotion(Vec2.Zero, 0f, load)));
}
#endif
