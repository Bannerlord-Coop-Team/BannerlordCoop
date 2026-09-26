using Common.Commands;
using E2E.Tests.Util;
using GameInterface.Services.MobileParties.Commands;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MobileParties;

public class MobilePartyDisorganizationTests : SyncTestBase
{
    public MobilePartyDisorganizationTests(ITestOutputHelper output) : base(output) { }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Expiry_OnlyServerClearsDisorganizationAndReplicates(bool expired)
    {
        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        TestEnvironment.FlushCoalescer();
        foreach (var instance in Clients.Append(Server))
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
                party._isDisorganized = true;
                party.VersionNo = 100;
                party._partyPureSpeedLastCheckVersion = party.GetVersionNoForBaseSpeedCalculation();
                party._disorganizedUntilTime = Campaign.Current.MapTimeTracker.Now + CampaignTime.Hours(expired ? -1 : 1);
            });

        foreach (var client in Clients)
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
                Assert.True(party.IsDisorganized);
                Assert.Equal(expired, party._disorganizedUntilTime.IsPast);
                int version = party.VersionNo;
                party.CheckIsDisorganized();
                Assert.True(party.IsDisorganized);
                Assert.Equal(version, party.VersionNo);
            });

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            party.CheckIsDisorganized();
        });
        TestEnvironment.FlushCoalescer();
        foreach (var instance in Clients.Append(Server))
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
                Assert.Equal(!expired, party.IsDisorganized);
                Assert.Equal(expired ? 101 : 100, party.VersionNo);
                Assert.Equal(expired, party._partyPureSpeedLastCheckVersion != party.GetVersionNoForBaseSpeedCalculation());
            });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ServerSet_InvalidatesClientSpeedCacheOnceWithoutRecalculatingExpiry(bool disorganized)
    {
        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        TestEnvironment.FlushCoalescer();
        var expectedExpiry = CampaignTime.Never;
        foreach (var instance in Clients.Append(Server))
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
                party._isDisorganized = !disorganized;
                party._disorganizedUntilTime = CampaignTime.HoursFromNow(7);
                party.VersionNo = 100;
                party._partyPureSpeedLastCheckVersion = party.GetVersionNoForBaseSpeedCalculation();
            });

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            party.SetDisorganized(disorganized);
            expectedExpiry = party._disorganizedUntilTime;
        });
        TestEnvironment.FlushCoalescer();
        foreach (var client in Clients)
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
                Assert.Equal(disorganized, party.IsDisorganized);
                Assert.Equal(101, party.VersionNo);
                Assert.NotEqual(party._partyPureSpeedLastCheckVersion, party.GetVersionNoForBaseSpeedCalculation());
                Assert.Equal(expectedExpiry, party._disorganizedUntilTime);
                party._partyPureSpeedLastCheckVersion = party.GetVersionNoForBaseSpeedCalculation();
            });

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            party.SetDisorganized(disorganized);
        });
        TestEnvironment.FlushCoalescer();
        foreach (var client in Clients)
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
                Assert.Equal(disorganized, party.IsDisorganized);
                Assert.Equal(101, party.VersionNo);
                Assert.Equal(party._partyPureSpeedLastCheckVersion, party.GetVersionNoForBaseSpeedCalculation());
                party.SetDisorganized(!disorganized);
                Assert.Equal(disorganized, party.IsDisorganized);
                Assert.Equal(101, party.VersionNo);
                Assert.Equal(expectedExpiry, party._disorganizedUntilTime);
            });
    }

    [Fact]
    public void DebugCommands_UseAuthoritativeActionAndObserveWithoutChangingCache()
    {
        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        TestEnvironment.FlushCoalescer();
        var args = new CoopCommandArgsFactory();
        Server.Call(() =>
        {
            var set = new MobilePartyDebugCommand.SetDisorganizedCoopCommand(Server.ObjectManager);
            Assert.False(set.ProcessCommand(args.FromValues(new[] { partyId, "invalid" })).Succeeded);
            Assert.False(set.ProcessCommand(args.FromValues(new[] { "missing-party", "true" })).Succeeded);
            Assert.True(set.ProcessCommand(args.FromValues(new[] { partyId, "true" })).Succeeded);
        });
        TestEnvironment.FlushCoalescer();
        foreach (var client in Clients)
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
                Assert.True(party.IsDisorganized);
                var version = party.VersionNo;
                var cachedVersion = party._partyPureSpeedLastCheckVersion;
                var inspect = new MobilePartyDebugCommand.DisorganizationCacheCoopCommand(client.ObjectManager);
                var result = inspect.ProcessCommand(args.FromValues(new[] { partyId, "false" }));
                Assert.True(result.Succeeded);
                Assert.Contains("IsDisorganized=True", result.Output);
                Assert.Contains($"DerivedVersion={party.GetVersionNoForBaseSpeedCalculation()} ", result.Output);
                Assert.Contains($"CacheVersion={cachedVersion} ", result.Output);
                Assert.Equal(version, party.VersionNo);
                Assert.Equal(cachedVersion, party._partyPureSpeedLastCheckVersion);
                Assert.False(inspect.ProcessCommand(args.FromValues(new[] { "missing-party", "false" })).Succeeded);
                Assert.False(inspect.ProcessCommand(args.FromValues(new[] { partyId, "invalid" })).Succeeded);
                var set = new MobilePartyDebugCommand.SetDisorganizedCoopCommand(client.ObjectManager);
                Assert.False(set.ProcessCommand(args.FromValues(new[] { partyId, "false" })).Succeeded);
                Assert.True(party.IsDisorganized);
                Assert.Equal(version, party.VersionNo);
                var evaluated = inspect.ProcessCommand(args.FromValues(new[] { partyId, "true" }));
                Assert.True(evaluated.Succeeded);
                Assert.Contains("Evaluate=True", evaluated.Output);
                Assert.Equal(party.GetVersionNoForBaseSpeedCalculation(), party._partyPureSpeedLastCheckVersion);
            });
    }

}
