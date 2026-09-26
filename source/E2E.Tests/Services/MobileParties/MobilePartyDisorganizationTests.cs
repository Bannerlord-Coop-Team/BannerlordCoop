using E2E.Tests.Util;
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
}
