using E2E.Tests.Util;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using Xunit.Abstractions;

namespace E2E.Tests.Services.StanceLinks;

public class StanceLinkSyncTests : SyncTestBase
{
    private string StanceLinkId;
    public StanceLinkSyncTests(ITestOutputHelper output) : base(output)
    {
        StanceLinkId = TestEnvironment.CreateRegisteredObject<StanceLink>();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ClanWarAndPeace_ReplicateAgainstClanOrKingdom(bool againstKingdom)
    {
        var firstId = TestEnvironment.CreateRegisteredObject<Clan>();
        var secondId = againstKingdom
            ? TestEnvironment.CreateRegisteredObject<Kingdom>()
            : TestEnvironment.CreateRegisteredObject<Clan>();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<IFaction>(firstId, out var first));
            Assert.True(Server.ObjectManager.TryGetObject<IFaction>(secondId, out var second));
            DeclareWarAction.ApplyInternal(first, second, DeclareWarAction.DeclareWarDetail.CausedByKingdomDecision);
        });
        AssertStance(true);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<IFaction>(firstId, out var first));
            Assert.True(Server.ObjectManager.TryGetObject<IFaction>(secondId, out var second));
            MakePeaceAction.ApplyInternal(first, second, 0, 0, MakePeaceAction.MakePeaceDetail.ByKingdomDecision);
        });
        AssertStance(false);

        void AssertStance(bool atWar)
        {
            foreach (var instance in Clients.Append(Server))
            {
                instance.Call(() =>
                {
                    Assert.True(instance.ObjectManager.TryGetObject<IFaction>(firstId, out var first));
                    Assert.True(instance.ObjectManager.TryGetObject<IFaction>(secondId, out var second));
                    Assert.Equal(atWar, FactionManager.IsAtWarAgainstFaction(first, second));
                });
            }
        }
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
