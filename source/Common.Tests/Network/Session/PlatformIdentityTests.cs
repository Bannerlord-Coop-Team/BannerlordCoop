using Common.Network.Session;
using System.Collections.Generic;
using Xunit;

namespace Common.Tests.Network.Session;

public class PlatformIdentityTests
{
    [Theory]
    [InlineData(" Steam ", " 42 ", "steam:42")]
    [InlineData("GOG", "42", "gog:42")]
    [InlineData("LOCAL", "installation-id", "local:installation-id")]
    public void Constructor_NormalizesProviderAndControllerId(
        string provider,
        string userId,
        string expectedControllerId)
    {
        var identity = new PlatformIdentity(provider, userId);

        Assert.True(identity.IsValid);
        Assert.Equal(expectedControllerId, identity.ControllerId);
        Assert.Equal(expectedControllerId, identity.ToString());
    }

    [Fact]
    public void Equality_IsolatesEqualIdsFromDifferentProviders()
    {
        var steam = new PlatformIdentity("steam", "42");
        var gog = new PlatformIdentity("gog", "42");
        var local = new PlatformIdentity("local", "42");
        var identities = new HashSet<PlatformIdentity> { steam, gog, local };

        Assert.Equal(3, identities.Count);
        Assert.NotEqual(steam, gog);
        Assert.NotEqual(gog, local);
        Assert.True(steam.IsStorefrontIdentity);
        Assert.True(gog.IsStorefrontIdentity);
        Assert.False(local.IsStorefrontIdentity);
    }

    [Theory]
    [InlineData("steam:76561198000000042", "steam", "76561198000000042")]
    [InlineData("GOG:12345", "gog", "12345")]
    [InlineData("local:4db2657ef545443b930c605d8c59891a", "local", "4db2657ef545443b930c605d8c59891a")]
    public void TryParseControllerId_RoundTripsProviderScopedIdentity(
        string controllerId,
        string expectedProvider,
        string expectedUserId)
    {
        Assert.True(PlatformIdentity.TryParseControllerId(controllerId, out var identity));
        Assert.Equal(expectedProvider, identity.Provider);
        Assert.Equal(expectedUserId, identity.UserId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("steam")]
    [InlineData(":42")]
    [InlineData("steam:")]
    public void TryParseControllerId_RejectsIncompleteValues(string controllerId)
    {
        Assert.False(PlatformIdentity.TryParseControllerId(controllerId, out var identity));
        Assert.False(identity.IsValid);
    }

    [Fact]
    public void TryMigrateLegacySteamControllerId_NumericId_AddsSteamNamespace()
    {
        Assert.True(PlatformIdentity.TryMigrateLegacySteamControllerId(
            "76561198000000042",
            out var migratedControllerId));
        Assert.Equal("steam:76561198000000042", migratedControllerId);
    }

    [Theory]
    [InlineData("steam:76561198000000042")]
    [InlineData("gog:76561198000000042")]
    [InlineData("PlayerOne")]
    public void TryMigrateLegacySteamControllerId_NonLegacyId_LeavesValueUnchanged(string controllerId)
    {
        Assert.False(PlatformIdentity.TryMigrateLegacySteamControllerId(
            controllerId,
            out var migratedControllerId));
        Assert.Equal(controllerId, migratedControllerId);
    }
}
