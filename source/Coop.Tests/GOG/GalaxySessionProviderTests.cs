using Common.Messaging;
using Common.Network.Session;
using Coop.GOG;
using Xunit;

namespace Coop.Tests.GOG;

public class GalaxySessionProviderTests
{
    [Fact]
    public void CreateClient_WhenGalaxyAuthenticationIsPending_KeepsBrowserVisible()
    {
        var sdk = new FakeGalaxySdk
        {
            LocalUserId = 0,
            CompleteAuthenticationImmediately = false,
        };
        using var messageBroker = new MessageBroker();
        using var provider = GalaxySessionProvider.CreateClient(
            sdk,
            messageBroker,
            new AllowSessionJoinRequestGate());

        Assert.False(provider.IsAvailable);
        Assert.True(provider.Browser.IsAvailable);
        Assert.Equal("GOG", provider.Browser.DisplayName);
        Assert.Equal(1, sdk.AuthenticationRequests);

        sdk.CompleteAuthentication(success: true);

        Assert.True(provider.IsAvailable);
    }

    private sealed class AllowSessionJoinRequestGate : ISessionJoinRequestGate
    {
        public bool CanStartJoin() => true;
    }
}
