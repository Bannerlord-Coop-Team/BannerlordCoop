using GameInterface.Services.TroopRosters.Patches;

namespace E2E.Tests.Services.TroopRosters;

public class TroopRosterMutationReportingTests
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    public void ClientMutationReporting_RequiresManagedRoster(
        bool isClient,
        bool isRegistered,
        bool expected)
    {
        Assert.Equal(
            expected,
            TroopRosterPatches.ShouldReportClientMutation(isClient, isRegistered));
    }
}
