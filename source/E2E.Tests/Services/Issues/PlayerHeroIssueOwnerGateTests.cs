using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.Heroes.Extensions;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Issues;

public class PlayerHeroIssueOwnerGateTests : IDisposable
{
    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;

    public PlayerHeroIssueOwnerGateTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    [Fact]
    public void APlayerHeroCannotHaveCampaignIssuesOnTheServer()
    {
        var heroId = TestEnvironment.CreateRegisteredObject<Hero>();
        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();

        Server.Call(() =>
        {
            Assert.True(Server.Resolve<IPlayerManager>().AddPlayer(new Player("player-A", heroId, partyId, "", "")));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(heroId, out var hero));

            Assert.False(hero.CanHaveCampaignIssues());
        });
    }

    [Fact]
    public void AnAiLordInAPlayersClanCannotHaveCampaignIssuesOnTheServer()
    {
        var heroId = TestEnvironment.CreateRegisteredObject<Hero>();
        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        var clanId = TestEnvironment.CreateRegisteredObject<Clan>();
        var lordId = TestEnvironment.CreateRegisteredObject<Hero>();

        Server.Call(() =>
        {
            Assert.True(Server.Resolve<IPlayerManager>().AddPlayer(new Player("player-A", heroId, partyId, clanId, "")));
            Assert.True(Server.ObjectManager.TryGetObject<Clan>(clanId, out var clan));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(lordId, out var lord));

            using (new AllowedThread())
            {
                lord._clan = clan;
            }

            Assert.False(lord.IsPlayerHero());
            Assert.False(lord.CanHaveCampaignIssues());
        });
    }

    [Fact]
    public void AHeroThatNoPlayerControlsCanStillHaveCampaignIssues()
    {
        var heroId = TestEnvironment.CreateRegisteredObject<Hero>();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(heroId, out var hero));

            Assert.True(hero.CanHaveCampaignIssues());
        });
    }
}
