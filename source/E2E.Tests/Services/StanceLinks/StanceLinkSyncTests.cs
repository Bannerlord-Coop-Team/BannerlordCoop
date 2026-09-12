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
    public void Server_StanceLink_Properties()
    {
        TestEnvironment.AssertProperty<StanceLink, int>(nameof(StanceLink.TroopCasualties1), 67);
        TestEnvironment.AssertProperty<StanceLink, int>(nameof(StanceLink.TroopCasualties2), 69);

        TestEnvironment.AssertProperty<StanceLink, int>(nameof(StanceLink.ShipCasualties1), 69);
        TestEnvironment.AssertProperty<StanceLink, int>(nameof(StanceLink.ShipCasualties2), 69);

        TestEnvironment.AssertProperty<StanceLink, int>(nameof(StanceLink.SuccessfulSieges1), 3);
        TestEnvironment.AssertProperty<StanceLink, int>(nameof(StanceLink.SuccessfulSieges2), 4);

        TestEnvironment.AssertProperty<StanceLink, int>(nameof(StanceLink.SuccessfulRaids1), 5);
        TestEnvironment.AssertProperty<StanceLink, int>(nameof(StanceLink.SuccessfulRaids2), 6);

        TestEnvironment.AssertField<StanceLink, int>(nameof(StanceLink._totalTributePaidFrom1To2), 10000);

        TestEnvironment.AssertField<StanceLink, int>(nameof(StanceLink._dailyTributeFrom1To2), 40);

        TestEnvironment.AssertProperty<StanceLink, int>(nameof(StanceLink.DailyTributeInstallments), 7);

        TestEnvironment.AssertProperty<StanceLink, int>(nameof(StanceLink.SuccessfulTownSieges1), 2);
        TestEnvironment.AssertProperty<StanceLink, int>(nameof(StanceLink.SuccessfulTownSieges2), 3);
    }
}