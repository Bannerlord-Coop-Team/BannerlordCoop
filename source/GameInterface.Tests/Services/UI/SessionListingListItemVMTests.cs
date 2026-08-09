using Common;
using Common.Network.Session;
using GameInterface.Services.UI;
using Xunit;

namespace GameInterface.Tests.Services.UI;

public class SessionListingListItemVMTests
{
    [Fact]
    public void CompatibleListing_ExposesDisplayDataAndPublishesOpaqueListingId()
    {
        var listingId = new SessionListingId("gog", "42");
        SessionListingId joined = default;
        var viewModel = new SessionListingListItemVM(new SessionListing
        {
            Id = listingId,
            OwnerName = " GOG Host ",
            ConnectedPlayers = 3,
            ProtocolVersion = SessionJoinInfo.CurrentVersion,
            ModVersion = ModInformation.BuildVersion,
            PasswordRequired = true,
        }, id => joined = id);

        Assert.Equal("GOG Host", viewModel.HostText);
        Assert.Equal("3", viewModel.ConnectedPlayersText);
        Assert.Equal("Password required", viewModel.PasswordText);
        Assert.False(viewModel.IsJoinDisabled);
        viewModel.ExecuteJoin();
        Assert.Equal(listingId, joined);
    }

    [Fact]
    public void DifferentModVersion_DisablesJoinAndShowsVersionHint()
    {
        bool joined = false;
        var viewModel = new SessionListingListItemVM(new SessionListing
        {
            Id = new SessionListingId("steam", "42"),
            ProtocolVersion = SessionJoinInfo.CurrentVersion,
            ModVersion = "different-build",
        }, _ => joined = true);

        Assert.True(viewModel.IsJoinDisabled);
        Assert.Equal("Incompatible", viewModel.StatusText);
        Assert.True(viewModel.IsStatusHintVisible);
        viewModel.ExecuteJoin();
        Assert.False(joined);
    }

    [Fact]
    public void DifferentProtocolVersion_DisablesJoin()
    {
        var viewModel = new SessionListingListItemVM(new SessionListing
        {
            Id = new SessionListingId("gog", "42"),
            ProtocolVersion = SessionJoinInfo.CurrentVersion + 1,
            ModVersion = ModInformation.BuildVersion,
        }, _ => { });

        Assert.True(viewModel.IsJoinDisabled);
        Assert.Equal("Incompatible", viewModel.StatusText);
    }

    [Fact]
    public void EmptyOwnerAndNegativePlayersUseSafeDisplayFallbacks()
    {
        var viewModel = new SessionListingListItemVM(new SessionListing
        {
            Id = new SessionListingId("gog", "42"),
            OwnerName = " ",
            ConnectedPlayers = -3,
            ProtocolVersion = SessionJoinInfo.CurrentVersion,
            ModVersion = ModInformation.BuildVersion,
        }, _ => { });

        Assert.Equal("Unknown host", viewModel.HostText);
        Assert.Equal("0", viewModel.ConnectedPlayersText);
    }
}
