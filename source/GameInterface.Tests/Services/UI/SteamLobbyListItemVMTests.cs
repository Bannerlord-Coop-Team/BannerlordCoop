using GameInterface.Services.UI;
using Xunit;

namespace GameInterface.Tests.Services.UI;

public class SteamLobbyListItemVMTests
{
    [Fact]
    public void CompatibleVersion_UsesNormalColorAndJoins()
    {
        ulong joinedLobby = 0;
        var viewModel = new SteamLobbyListItemVM(
            42,
            Common.Network.Session.SessionJoinInfo.CurrentVersion,
            Common.ModInformation.BuildVersion,
            false,
            true,
            lobbyId => joinedLobby = lobbyId);

        Assert.Equal("#F4E1C4FF", viewModel.VersionColor);
        Assert.False(viewModel.IsJoinDisabled);

        viewModel.ExecuteJoin();

        Assert.Equal(42UL, joinedLobby);
    }

    [Fact]
    public void IncompatibleVersion_UsesRedAndDoesNotJoin()
    {
        ulong joinedLobby = 0;
        var viewModel = new SteamLobbyListItemVM(
            42,
            Common.Network.Session.SessionJoinInfo.CurrentVersion,
            "different-build",
            false,
            false,
            lobbyId => joinedLobby = lobbyId);

        Assert.Equal("#FF5555FF", viewModel.VersionColor);
        Assert.True(viewModel.IsJoinDisabled);

        viewModel.ExecuteJoin();

        Assert.Equal(0UL, joinedLobby);
    }
}
