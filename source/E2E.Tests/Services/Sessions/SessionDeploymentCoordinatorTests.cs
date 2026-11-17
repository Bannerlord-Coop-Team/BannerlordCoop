using Missions.Sessions;

namespace E2E.Tests.Services.Sessions;

public class SessionDeploymentCoordinatorTests
{
    [Fact]
    public void Coordinator_TeamSetupIsOverOnlyWithAMission()
    {
        var coordinator = new SessionDeploymentCoordinator();

        Assert.False(coordinator.IsTeamSetupOver);
    }
}
