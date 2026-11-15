using Missions.Sessions;

namespace E2E.Tests.Services.Sessions;

public class SessionDeploymentCoordinatorTests
{
    [Fact]
    public void Coordinator_IsLiveFromStart()
    {
        var coordinator = new SessionDeploymentCoordinator();

        Assert.True(coordinator.IsActivated);
        Assert.True(coordinator.IsCommitted);
        Assert.False(coordinator.ShouldWithhold(true));
        Assert.False(coordinator.ShouldWithhold(false));
        Assert.False(coordinator.OnLocalDeploymentFinished());
    }

    [Fact]
    public void Coordinator_TeamSetupIsOverOnlyWithAMission()
    {
        var coordinator = new SessionDeploymentCoordinator();

        Assert.False(coordinator.IsTeamSetupOver);
    }
}
