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
        foreach (var instance in Clients.Append(Server))
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
                party._isDisorganized = true;
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
            });
    }
}
