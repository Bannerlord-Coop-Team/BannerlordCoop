using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.Actions.Messages;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Armies;

/// <summary>A player's disband request is applied on the server, which has no player clan of its own.</summary>
public class ArmyDisbandCostTests : IDisposable
{
    private readonly E2ETestEnvironment environment;
    private EnvironmentInstance Server => environment.Server;
    private EnvironmentInstance Member => environment.Clients.First();

    public ArmyDisbandCostTests(ITestOutputHelper output)
    {
        environment = new E2ETestEnvironment(output);
    }

    public void Dispose() => environment.Dispose();

    [Fact]
    public void DisbandRequest_DisbandsTheArmyAndChargesTheDisbandingClan()
    {
        var dispersals = new List<bool>();
        var armyId = environment.CreateRegisteredObject<Army>();
        var memberPartyId = environment.CreateRegisteredObject<MobileParty>();
        float influenceBefore = 0;
        Server.Call(() =>
        {
            // Like a dedicated server: no player clan, so vanilla's Clan.PlayerClan is null.
            Assert.Null(Clan.PlayerClan);

            var army = Server.GetRegisteredObject<Army>(armyId);
            var member = Server.GetRegisteredObject<MobileParty>(memberPartyId);
            member.Army = army;
            member.AttachedTo = army.LeaderParty;
            var memberHero = member.LeaderHero;
            if (memberHero.Clan == null) memberHero.Clan = GameObjectCreator.CreateInitializedObject<Clan>();
            memberHero.Clan.Influence = 100f;
            influenceBefore = memberHero.Clan.Influence;

            Assert.True(Server.ObjectManager.TryGetId(memberHero, out var heroId));
            var playerManager = Server.Resolve<IPlayerManager>();
            Assert.True(playerManager.AddPlayer(new Player("Member", heroId, memberPartyId, null, null)));
            playerManager.SetPeer("Member", Member.NetPeer);

            // The dedicated server runs vanilla's army wait behavior, which opens a menu for its own player's army
            ObjectHelper.SkipConstructor<PlayerArmyWaitBehavior>().RegisterEvents();
            CampaignEvents.ArmyDispersed.AddNonSerializedListener(this, (_, _, isPlayersArmy) => dispersals.Add(isPlayersArmy));
        });

        Server.SimulateMessage(Member.NetPeer, new NetworkDisbandArmyApplyInternal(armyId, memberPartyId));
        Server.PumpGameThread();

        Server.Call(() =>
        {
            var member = Server.GetRegisteredObject<MobileParty>(memberPartyId);
            Assert.Null(member.Army);
            Assert.True(member.LeaderHero.Clan.Influence < influenceBefore);
            Assert.Null(Clan.PlayerClan);
            // A menu on the server zeroes the campaign dt before TickPartialHourlyAi, so lord and bandit AI stop thinking
            Assert.Null(Campaign.Current.GameMenuManager.NextGameMenuId);
            Assert.False(Assert.Single(dispersals));
        });
    }
}
