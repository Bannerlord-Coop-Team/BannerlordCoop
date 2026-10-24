#if COOP_NAVAL
using HarmonyLib;
using Missions.Messages;
using Missions.Naval;
using NavalDLC.Missions.Objects;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using Xunit;
using NavalPhysicsScript = NavalDLC.Missions.NavalPhysics.NavalPhysics;

namespace Coop.Tests.Missions.Battles;

/// <summary>R45 roll: a tracked copy's owner crew load, from Track to Forget as the crew load patch finds it, and the load it keeps.</summary>
public class NavalSteeredCopiesTests
{
    [Fact]
    public void Track_MakesTheCopyReachableFromItsPhysicsUntilForget()
    {
        var physics = ShipTestHulls.Uninitialized<NavalPhysicsScript>();
        var ship = ShipTestHulls.Uninitialized<MissionShip>();
        ship._physics = physics;
        AccessTools.Field(typeof(ScriptComponentBehavior), "_gameEntity").SetValue(ship, NavalShipEngineTests.ValidEntity());
        var copies = new NavalSteeredCopies(new CopySteeringKnobs());

        try
        {
            copies.Track(ship);
            Assert.True(NavalForeignHulls.Contains(ship));
            Assert.True(NavalForeignHulls.TryGetCopy(physics, out var copy));
            Assert.Null(copy.CrewLoad);
            // The owner decides a copy, so it is left without an auto-updated controller.
            Assert.False(ship._autoUpdateController);

            copies.Forget(ship);
            Assert.False(NavalForeignHulls.Contains(ship));
            Assert.False(NavalForeignHulls.TryGetCopy(physics, out _));
        }
        finally
        {
            copies.Forget(ship);
        }
    }

    [Fact]
    public void KeptLoad_TakesEachSampledLoadAndKeepsTheLastWithoutOne()
    {
        var load = BattleShipCrewLoad.Create(480f, new Vec3(0.5f, -4f, 1.5f));

        Assert.Same(load, NavalSteeredCopies.KeptLoad(null, load));
        Assert.Same(BattleShipCrewLoad.Empty, NavalSteeredCopies.KeptLoad(load, BattleShipCrewLoad.Empty));
        // A sample without a load keeps the last one.
        Assert.Same(BattleShipCrewLoad.Empty, NavalSteeredCopies.KeptLoad(BattleShipCrewLoad.Empty, null));
        Assert.Null(NavalSteeredCopies.KeptLoad(null, null));
    }
}
#endif
