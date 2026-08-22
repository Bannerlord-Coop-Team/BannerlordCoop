using Common.Network.Session;
using Xunit;

namespace Common.Tests.Network.Session;

public class MissionPeerRolesTests
{
    [Fact]
    public void Resolve_SameProviderChoosesExactlyOneConnector()
    {
        var lower = new PlatformIdentity("gog", "100");
        var higher = new PlatformIdentity("gog", "200");

        Assert.Equal(MissionPeerRole.Listen, MissionPeerRoles.Resolve(lower, higher));
        Assert.Equal(MissionPeerRole.Connect, MissionPeerRoles.Resolve(higher, lower));
    }

    [Fact]
    public void Resolve_DifferentProvidersCannotCreatePlatformLink()
    {
        Assert.Equal(
            MissionPeerRole.Unavailable,
            MissionPeerRoles.Resolve(
                new PlatformIdentity("steam", "42"),
                new PlatformIdentity("gog", "42")));
    }

    [Theory]
    [InlineData("", "", "gog", "42")]
    [InlineData("gog", "42", "", "")]
    [InlineData("steam", "42", "steam", "42")]
    public void Resolve_InvalidOrEqualIdentitiesAreUnavailable(
        string localProvider,
        string localUserId,
        string remoteProvider,
        string remoteUserId)
    {
        Assert.Equal(
            MissionPeerRole.Unavailable,
            MissionPeerRoles.Resolve(
                new PlatformIdentity(localProvider, localUserId),
                new PlatformIdentity(remoteProvider, remoteUserId)));
    }
}
