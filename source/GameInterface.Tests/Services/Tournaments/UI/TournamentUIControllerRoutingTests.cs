using GameInterface.Services.Tournaments.Data;
using GameInterface.Services.Tournaments.UI;
using Xunit;

namespace GameInterface.Tests.Services.Tournaments.UI;

public class TournamentUIControllerRoutingTests
{
    [Fact]
    public void DifferentTownSnapshot_RefreshesCurrentMenuWithoutSwitching()
    {
        var route = TournamentUIController.GetMenuRoute(
            false,
            CoopTournamentCampaignBehavior.TownArenaMenuId,
            TournamentSessionPhase.AwaitingChoices,
            false,
            false);

        Assert.Equal(TournamentUIController.MenuRoute.Refresh, route);
    }

    [Fact]
    public void EnrolledPreparationSnapshot_OpensPreparationMenu()
    {
        var route = TournamentUIController.GetMenuRoute(
            true,
            CoopTournamentCampaignBehavior.TownArenaMenuId,
            TournamentSessionPhase.Preparation,
            false,
            true);

        Assert.Equal(TournamentUIController.MenuRoute.Preparation, route);
    }

    [Fact]
    public void ActiveSnapshotForNoncompetitor_OpensActiveMenu()
    {
        var route = TournamentUIController.GetMenuRoute(
            true,
            CoopTournamentCampaignBehavior.PreparationMenuId,
            TournamentSessionPhase.LiveMatch,
            false,
            false);

        Assert.Equal(TournamentUIController.MenuRoute.Active, route);
    }

    [Fact]
    public void CompletedSnapshot_LeavesCustomTournamentMenu()
    {
        var route = TournamentUIController.GetMenuRoute(
            true,
            CoopTournamentCampaignBehavior.ActiveMenuId,
            TournamentSessionPhase.Completed,
            true,
            false);

        Assert.Equal(TournamentUIController.MenuRoute.TownCenter, route);
    }

    [Fact]
    public void AcceptedPreparationLeave_LeavesPreparationMenu()
    {
        var route = TournamentUIController.GetMenuRoute(
            true,
            CoopTournamentCampaignBehavior.PreparationMenuId,
            TournamentSessionPhase.Preparation,
            false,
            false);

        Assert.Equal(TournamentUIController.MenuRoute.TownCenter, route);
    }

    [Fact]
    public void CurrentTownTombstone_LeavesCustomTournamentMenu()
    {
        var route = TournamentUIController.GetRemovedSessionMenuRoute(
            true,
            CoopTournamentCampaignBehavior.PreparationMenuId);

        Assert.Equal(TournamentUIController.MenuRoute.TownCenter, route);
    }

    [Fact]
    public void DifferentTownTombstone_OnlyRefreshesCurrentMenu()
    {
        var route = TournamentUIController.GetRemovedSessionMenuRoute(
            false,
            CoopTournamentCampaignBehavior.PreparationMenuId);

        Assert.Equal(TournamentUIController.MenuRoute.Refresh, route);
    }
}
