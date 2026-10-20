#if DEBUG
using HarmonyLib;
using Missions.Naval;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace Coop.Tests.Missions.Battles;

public class LateHullWeaponAiPatchTests
{
    [Fact]
    public void Target_RangedSiegeWeaponReadiesItsAiOnDeploymentFinished()
    {
        Assert.NotNull(AccessTools.DeclaredMethod(typeof(RangedSiegeWeapon), nameof(RangedSiegeWeapon.OnDeploymentFinished)));
        Assert.NotNull(AccessTools.DeclaredMethod(typeof(RangedSiegeWeaponAi), nameof(RangedSiegeWeaponAi.InitializeThreatSeeker)));
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, false)]
    public void ReadiesWeaponAi_OnlyForHullsSpawnedAfterDeploymentInACoopNavalBattle(bool coopNavalBattle, bool deploymentFinished,
        bool expected)
    {
        Assert.Equal(expected, LateHullWeaponAiPatch.ReadiesWeaponAi(coopNavalBattle, deploymentFinished));
    }
}
#endif
