using E2E.Tests.Util;
using TaleWorlds.CampaignSystem;
using Xunit.Abstractions;

namespace E2E.Tests.Services.StanceLinks;

public class StanceLinkSyncTests : SyncTestBase
{
    private string StanceLinkId;
    public StanceLinkSyncTests(ITestOutputHelper output) : base(output)
    {
        StanceLinkId = TestEnvironment.CreateRegisteredObject<StanceLink>();
    }

    [Fact]
    public void Server_StanceLink_AutosyncProperties()
    {
        TestEnvironment.AssertProperty<StanceLink, int>(nameof(StanceLink.ShipCasualties1), 69);
        TestEnvironment.AssertProperty<StanceLink, int>(nameof(StanceLink.ShipCasualties2), 69);

        TestEnvironment.AssertField<StanceLink, int>(nameof(StanceLink._totalTributePaidFrom1To2), 10000);

        TestEnvironment.AssertField<StanceLink, int>(nameof(StanceLink._dailyTributeFrom1To2), 40);

        TestEnvironment.AssertProperty<StanceLink, int>(nameof(StanceLink.DailyTributeInstallments), 7);
    }

    [Fact]
    public void Server_StanceLink_Properties()
    {
        var stanceLinkId = TestEnvironment.CreateRegisteredObject<StanceLink>();
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<StanceLink>(stanceLinkId, out var stance));

            FactionManager.DeclareWar(stance.Faction1, stance.Faction2);
            Assert.True(stance.IsAtWar);

            stance.TroopCasualties1 = 100;
            stance.TroopCasualties2 = 200;
            stance.SuccessfulSieges1 = 13;
            stance.SuccessfulSieges2 = 11;
            stance.SuccessfulRaids1 = 10;
            stance.SuccessfulRaids2 = 4;
            stance.SuccessfulTownSieges1 = 2;
            stance.SuccessfulTownSieges2 = 1;
        });
        foreach(var client in Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<StanceLink>(stanceLinkId, out var clientStance));

                Assert.Equal(100, clientStance.TroopCasualties1);
                Assert.Equal(200, clientStance.TroopCasualties2);
                Assert.Equal(13, clientStance.SuccessfulSieges1);
                Assert.Equal(11, clientStance.SuccessfulSieges2);
                Assert.Equal(10, clientStance.SuccessfulRaids1);
                Assert.Equal(4, clientStance.SuccessfulRaids2);
                Assert.Equal(2, clientStance.SuccessfulTownSieges1);
                Assert.Equal(1, clientStance.SuccessfulTownSieges2);
            });
        }
    }
}