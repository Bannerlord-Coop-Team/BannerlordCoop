using System;
using System.Reflection;
using GameInterface.Services.MapEvents;
using Missions.Battles;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace E2E.Tests.Services.Missions;

/// <summary>
/// Guards the deployment-controller wiring for foreign teams. The fix must run inside the coop controller's
/// side-setup sequence; globally falsifying <c>DefaultMissionDeploymentPlan.IsPlanMade</c> lets consumers use
/// a plan that does not exist.
/// </summary>
public class CoopBattleDeploymentMissionControllerTests
{
    [Fact]
    public void CoopControllers_OverrideSideSetup_WithoutGlobalPlanOverride()
    {
        AssertSideSetupOverride(typeof(CoopBattleDeploymentMissionController));
        AssertSideSetupOverride(typeof(CoopSiegeDeploymentMissionController));

        Type removedPatch = typeof(BattleSpawnGate).Assembly
            .GetType("GameInterface.Services.MapEvents.Patches.CoopEmptyTeamDeploymentPatch");
        Assert.Null(removedPatch);
    }

    private static void AssertSideSetupOverride(Type controllerType)
    {
        MethodInfo setup = controllerType.GetMethod(
            "OnSetupTeamsOfSide",
            BindingFlags.Instance | BindingFlags.Public,
            binder: null,
            new[] { typeof(BattleSideEnum) },
            modifiers: null);

        Assert.NotNull(setup);
        Assert.Equal(controllerType, setup.DeclaringType);
    }
}
