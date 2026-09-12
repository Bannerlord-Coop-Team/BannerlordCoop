using GameInterface.Services.Tournaments;
using Xunit;

namespace GameInterface.Tests.Services.Tournaments;

public class TournamentNativeRemovalAuthorizationTests
{
    [Fact]
    public void Authorization_IsScopedToTownAndSupportsNesting()
    {
        var authorization = new TournamentNativeRemovalAuthorization();

        Assert.False(authorization.IsAuthorized("town-a"));
        using (authorization.Authorize("town-a"))
        {
            Assert.True(authorization.IsAuthorized("town-a"));
            Assert.False(authorization.IsAuthorized("town-b"));
            using (authorization.Authorize("town-a"))
                Assert.True(authorization.IsAuthorized("town-a"));
            Assert.True(authorization.IsAuthorized("town-a"));
        }
        Assert.False(authorization.IsAuthorized("town-a"));
    }
}