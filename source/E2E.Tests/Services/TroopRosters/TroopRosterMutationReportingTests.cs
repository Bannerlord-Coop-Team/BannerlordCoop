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

    [Theory]
    [InlineData(true, 0, 2, true)]
    [InlineData(true, 1, 1, true)]
    [InlineData(true, 3, -1, false)]
    [InlineData(false, 3, 1, false)]
    public void HeroAdditionGuard_RejectsOnlyPositiveHeroDuplicates(
        bool isHero,
        int currentCount,
        int countChange,
        bool expected)
    {
        Assert.Equal(
            expected,
            TroopRosterPatches.ShouldRejectHeroAddition(isHero, currentCount, countChange));
    }
}
