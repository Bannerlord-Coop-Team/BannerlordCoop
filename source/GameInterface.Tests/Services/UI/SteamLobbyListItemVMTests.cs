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
            "Test Host",
            3,
            Common.Network.Session.SessionJoinInfo.CurrentVersion,
            Common.ModInformation.BuildVersion,
            false,
            true,
            lobbyId => joinedLobby = lobbyId);

        Assert.Equal("Test Host", viewModel.HostText);
        Assert.Equal(3, viewModel.ConnectedPlayers);
        Assert.Equal("3", viewModel.ConnectedPlayersText);
        Assert.Equal("Compatible", viewModel.StatusText);
        Assert.Equal("#F4E1C4FF", viewModel.StatusColor);
        Assert.True(viewModel.IsCompatibleStatusVisible);
        Assert.False(viewModel.IsStatusHintVisible);
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
            "Test Host",
            3,
            Common.Network.Session.SessionJoinInfo.CurrentVersion,
            "different-build",
            false,
            false,
            lobbyId => joinedLobby = lobbyId);

        Assert.Equal("Incompatible", viewModel.StatusText);
        Assert.Equal("#FF8080FF", viewModel.StatusColor);
        Assert.False(viewModel.IsCompatibleStatusVisible);
        Assert.True(viewModel.IsStatusHintVisible);
        Assert.Equal(
            $"The host's version is different-build while your version is {Common.ModInformation.BuildVersion}.",
            viewModel.StatusHint.HintText.ToString());
        Assert.True(viewModel.IsJoinDisabled);

        viewModel.ExecuteJoin();

        Assert.Equal(0UL, joinedLobby);
    }

    [Fact]
    public void IncompatibleProtocol_ExplainsProtocolMismatch()
    {
        var viewModel = new SteamLobbyListItemVM(
            42,
            "Test Host",
            3,
            Common.Network.Session.SessionJoinInfo.CurrentVersion + 1,
            Common.ModInformation.BuildVersion,
            false,
            false,
            _ => { });

        Assert.Equal(
            $"The host's protocol version is {Common.Network.Session.SessionJoinInfo.CurrentVersion + 1} " +
            $"while your protocol version is {Common.Network.Session.SessionJoinInfo.CurrentVersion}.",
            viewModel.StatusHint.HintText.ToString());
    }

    [Fact]
    public void MissingPersonaName_UsesUnknownHostFallback()
    {
        var viewModel = new SteamLobbyListItemVM(
            42,
            string.Empty,
            3,
            Common.Network.Session.SessionJoinInfo.CurrentVersion,
            Common.ModInformation.BuildVersion,
            false,
            true,
            _ => { });

        Assert.Equal("Unknown host", viewModel.HostText);
    }
}
