namespace E2E.Tests.Services.Missions;

/// <summary>Checks configuration-specific inclusion, not naval scenario behavior.</summary>
public sealed class NavalLabBuildConfigurationTests
{
#if DEBUG
    [Fact]
    public void Debug_IncludesNavalScenariosAndProductionHandlers()
    {
        Assert.NotNull(typeof(global::Missions.MissionModule).Assembly.GetType("Missions.Battles.NavalLabCoordinator"));
        Assert.NotNull(typeof(NavalLabBuildConfigurationTests).Assembly.GetType(
            "E2E.Tests.Services.Missions.NavalLabRoutingTests"));
        Assert.NotNull(typeof(NavalLabBuildConfigurationTests).Assembly.GetType(
            "E2E.Tests.Services.Missions.NavalLabLifecycleTests"));
    }
#else
    [Fact]
    public void Release_ExcludesNavalScenariosAndProductionHandlers_NotNavalScenarioCoverage()
    {
        Assert.Null(typeof(global::Missions.MissionModule).Assembly.GetType("Missions.Battles.NavalLabCoordinator"));
        Assert.Null(typeof(NavalLabBuildConfigurationTests).Assembly.GetType(
            "E2E.Tests.Services.Missions.NavalLabRoutingTests"));
        Assert.Null(typeof(NavalLabBuildConfigurationTests).Assembly.GetType(
            "E2E.Tests.Services.Missions.NavalLabLifecycleTests"));
    }
#endif
}
