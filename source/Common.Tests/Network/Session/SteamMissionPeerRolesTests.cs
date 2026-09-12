using Common.Network.Session;
using Xunit;

namespace Common.Tests.Network.Session;

public class SteamMissionPeerRolesTests
{
    [Fact]
    public void Resolve_AssignsExactlyOneConnector()
    {
        Assert.Equal(SteamMissionPeerRole.Listen, SteamMissionPeerRoles.Resolve(10, 20));
        Assert.Equal(SteamMissionPeerRole.Connect, SteamMissionPeerRoles.Resolve(20, 10));
    }

    [Theory]
    [InlineData(0ul, 20ul)]
    [InlineData(20ul, 0ul)]
    [InlineData(20ul, 20ul)]
    public void Resolve_RejectsMissingOrEqualIdentities(ulong localSteamId, ulong remoteSteamId)
    {
        Assert.Equal(SteamMissionPeerRole.Unavailable,
            SteamMissionPeerRoles.Resolve(localSteamId, remoteSteamId));
    }
}
