#if DEBUG
using HarmonyLib;
using Missions.Naval;
using NavalDLC.Missions.MissionLogics;
using NavalDLC.Missions.Objects;
using NavalDLC.Missions.Objects.UsableMachines;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.ComponentInterfaces;
using Xunit;

namespace Coop.Tests.Missions.Battles;

/// <summary>Late hulls get the deployment steps vanilla runs only on hulls spawned before deployment ends.</summary>
public class LateHullDeploymentPatchTests
{
    // The postfix hooks the formation SpawnShip overload every NavalShipsLogic spawn funnels through.
    [Fact]
    public void Target_SpawnShipFormationOverloadExists()
    {
        Assert.NotNull(AccessTools.DeclaredMethod(typeof(NavalShipsLogic), nameof(NavalShipsLogic.SpawnShip),
            new[] { typeof(Formation), typeof(MatrixFrame).MakeByRefType(), typeof(bool), typeof(bool) }));
    }

    // The vanilla deployment steps the postfix replays still exist with the signatures it calls.
    [Fact]
    public void Target_VanillaDeploymentStepsExist()
    {
        Assert.NotNull(AccessTools.DeclaredMethod(typeof(RangedSiegeWeapon), nameof(RangedSiegeWeapon.OnDeploymentFinished)));
        Assert.NotNull(AccessTools.DeclaredMethod(typeof(RangedSiegeWeaponAi), nameof(RangedSiegeWeaponAi.InitializeThreatSeeker)));
        Assert.NotNull(AccessTools.DeclaredMethod(typeof(ShipControllerMachine), nameof(ShipControllerMachine.OnDeploymentFinished)));
        Assert.NotNull(AccessTools.DeclaredMethod(typeof(MissionShipParametersModel),
            nameof(MissionShipParametersModel.CalculateMainDeckCrewSize), new[] { typeof(IShipOrigin), typeof(Agent) }));
        Assert.NotNull(AccessTools.DeclaredPropertySetter(typeof(MissionShip), nameof(MissionShip.CrewSizeOnMainDeck)));
    }

    // Only a hull spawned after deployment in a co-op battle misses vanilla's pass, so only that one gets the replay.
    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, false)]
    public void FinishesDeployment_OnlyForHullsSpawnedAfterDeploymentInACoopNavalBattle(bool coopNavalBattle, bool deploymentFinished,
        bool expected)
    {
        Assert.Equal(expected, LateHullDeploymentPatch.FinishesDeployment(coopNavalBattle, deploymentFinished));
    }
}
#endif
