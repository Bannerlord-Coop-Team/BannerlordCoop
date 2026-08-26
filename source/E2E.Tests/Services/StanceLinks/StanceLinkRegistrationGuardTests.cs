using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.StanceLinks.Messages;
using TaleWorlds.CampaignSystem;
using Xunit.Abstractions;

namespace E2E.Tests.Services.StanceLinks;

public class StanceLinkRegistrationGuardTests : IDisposable
{
    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;
    private EnvironmentInstance FirstClient => TestEnvironment.Clients.First();

    public StanceLinkRegistrationGuardTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
    }

    public void Dispose() => TestEnvironment.Dispose();

    [Fact]
    public void EliminatedFaction_RepeatedServerLookups_NeverPublishAcrossMultipleCalls()
    {
        Server.Call(() =>
        {
            var faction1 = Kingdom.CreateKingdom("guard_elim_kingdom_3");
            var faction2 = Kingdom.CreateKingdom("guard_elim_kingdom_4");
            faction2.DeactivateKingdom();

            for (int i = 0; i < 10; i++)
            {
                Assert.NotNull(FactionManager.Instance.GetStanceLinkInternal(faction1, faction2));
            }
        });

        Assert.Empty(Server.InternalMessages.GetMessages<RequestStanceLinkConstructed>());
    }

    [Fact]
    public void EliminatedFaction_RepeatedClientLookups_NeverPublishAcrossMultipleCalls()
    {
        string faction1Id = null;
        string faction2Id = null;
        Server.Call(() =>
        {
            var faction1 = Kingdom.CreateKingdom("guard_elim_kingdom_3");
            var faction2 = Kingdom.CreateKingdom("guard_elim_kingdom_4");
            faction2.DeactivateKingdom();

            Assert.True(Server.ObjectManager.TryGetId(faction1, out faction1Id));
            Assert.True(Server.ObjectManager.TryGetId(faction2, out faction2Id));
        });

        FirstClient.Call(() =>
        {

            Assert.True(FirstClient.ObjectManager.TryGetObject<IFaction>(faction1Id, out var clientFaction1));
            Assert.True(FirstClient.ObjectManager.TryGetObject<IFaction>(faction2Id, out var clientFaction2));

            for (int i = 0; i < 10; i++)
            {
                Assert.NotNull(FactionManager.Instance.GetStanceLinkInternal(clientFaction1, clientFaction2));
            }
        });
        Assert.Empty(FirstClient.NetworkSentMessages.GetMessages<StanceLinkConstructed>());
        Assert.Empty(Server.NetworkSentMessages.GetMessages<StanceLinkConstructed>());
    }


    [Fact]
    public void BanditMismatchedFaction_Server_NeverPublishesRequestStanceLinkConstructed()
    {
        Server.Call(() =>
        {
            var kingdom = Kingdom.CreateKingdom("guard_bandit_kingdom_1");
            var banditClan = Clan.CreateClan("guard_bandit_clan_1");
            banditClan.IsBanditFaction = true;

            Assert.True(banditClan.IsBanditFaction);
            Assert.False(kingdom.IsBanditFaction);

            Assert.NotNull(FactionManager.Instance.GetStanceLinkInternal(kingdom, banditClan));
        });

        Assert.Empty(Server.InternalMessages.GetMessages<RequestStanceLinkConstructed>());
    }

    [Fact]
    public void BanditMisMatchedFaction_Client_NeverPublishesRequestStanceLinkConstructed()
    {
        string kingdomId = null;
        string banditClanId = null;
        Server.Call(() =>
        {
            var kingdom = Kingdom.CreateKingdom("guard_bandit_kingdom_1");
            var banditClan = Clan.CreateClan("guard_bandit_clan_1");
            banditClan.IsBanditFaction = true;

            Assert.True(banditClan.IsBanditFaction);
            Assert.False(kingdom.IsBanditFaction);
            Server.ObjectManager.TryGetId(kingdom, out kingdomId);
            Server.ObjectManager.TryGetId(banditClan, out banditClanId);
        });

        FirstClient.Call(() =>
        {
            FirstClient.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom);
            FirstClient.ObjectManager.TryGetObject<Clan>(banditClanId, out var banditClan);
            banditClan.IsBanditFaction = true;
            Assert.NotNull(FactionManager.Instance.GetStanceLinkInternal(kingdom, banditClan));
            Assert.True(banditClan.IsBanditFaction);
            Assert.False(kingdom.IsBanditFaction);
        });
        Assert.Empty(FirstClient.InternalMessages.GetMessages<RequestStanceLinkConstructed>());
    }

    [Fact]
    public void NormalPair_Client_SendsRequestExactlyOnceAcrossRepeatedLookups()
    {
        var server = Server;
        var client = FirstClient;
        string faction1Id = null;
        string faction2Id = null;

        server.Call(() =>
        {
            var faction1 = Kingdom.CreateKingdom("guard_client_normal_kingdom_1");
            var faction2 = Kingdom.CreateKingdom("guard_client_normal_kingdom_2");

            Assert.True(server.ObjectManager.TryGetId(faction1, out faction1Id));
            Assert.True(server.ObjectManager.TryGetId(faction2, out faction2Id));
        });

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<IFaction>(faction1Id, out var clientFaction1));
            Assert.True(client.ObjectManager.TryGetObject<IFaction>(faction2Id, out var clientFaction2));

            var firstLookup = FactionManager.Instance.GetStanceLinkInternal(clientFaction1, clientFaction2);

            var secondLookup = FactionManager.Instance.GetStanceLinkInternal(clientFaction1, clientFaction2);

            Assert.Same(firstLookup, secondLookup);
        });

        Assert.Single(client.NetworkSentMessages.GetMessages<StanceLinkConstructed>());
    }
}