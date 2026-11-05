using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.Actions.Messages;
using GameInterface.Services.Armies.Messages;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Armies;

/// <summary>The server changes an army's members and cohesion only on the request of the party's player or the army leader's.</summary>
public class ArmyRequestSenderTests : IDisposable
{
    private readonly E2ETestEnvironment environment;
    private EnvironmentInstance Server => environment.Server;
    private EnvironmentInstance Member => environment.Clients.First();
    private EnvironmentInstance Outsider => environment.Clients.Last();

    private readonly string armyId;
    private string leaderPartyId;
    private readonly string memberPartyId;
    private readonly string outsiderPartyId;

    public ArmyRequestSenderTests(ITestOutputHelper output)
    {
        environment = new E2ETestEnvironment(output);
        armyId = environment.CreateRegisteredObject<Army>();
        memberPartyId = environment.CreateRegisteredObject<MobileParty>();
        outsiderPartyId = environment.CreateRegisteredObject<MobileParty>();
        Server.Call(() =>
        {
            var army = Get<Army>(armyId);
            var member = Get<MobileParty>(memberPartyId);
            member.Army = army;
            member.AttachedTo = army.LeaderParty;
            Assert.True(Server.ObjectManager.TryGetId(army.LeaderParty, out string leaderId));
            leaderPartyId = leaderId;

            Register("Member", Get<MobileParty>(memberPartyId), Member);
            Register("Outsider", Get<MobileParty>(outsiderPartyId), Outsider);
        });
    }

    public void Dispose() => environment.Dispose();

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void PartyIsRemovedFromTheArmyOnlyOnItsOwnPlayersRequest(bool fromMember)
    {
        Send(fromMember ? Member : Outsider, new NetworkRemovePartyInArmy(armyId, memberPartyId, string.Empty));

        Server.Call(() => Assert.Equal(fromMember, Get<MobileParty>(memberPartyId).Army == null));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void PartyIsAddedToTheArmyOnlyOnItsOwnPlayersRequest(bool ownParty)
    {
        var partyId = ownParty ? outsiderPartyId : environment.CreateRegisteredObject<MobileParty>();

        Send(Outsider, new NetworkAddMobilePartyInArmy(armyId, partyId, false));

        Server.Call(() => Assert.Equal(ownParty, Get<MobileParty>(partyId).Army == Get<Army>(armyId)));
    }

    [Fact]
    public void CohesionBoostFromAPlayerWhoDoesNotLeadTheArmy_IsRefused()
    {
        float cohesion = 0;
        Server.Call(() =>
        {
            var army = Get<Army>(armyId);
            var leader = army.LeaderParty.LeaderHero;
            if (leader.Clan == null) leader.Clan = GameObjectCreator.CreateInitializedObject<Clan>();
            leader.Clan.Influence = 1000f;
            army.Cohesion = 20f;
            cohesion = army.Cohesion;
        });

        Send(Outsider, new NetworkPlayerBoostedArmyCohesion(leaderPartyId, 10f, 0));

        Server.Call(() => Assert.Equal(cohesion, Get<Army>(armyId).Cohesion));
    }

    [Fact]
    public void CohesionBoostFromTheArmyLeader_IsApplied()
    {
        float cohesion = 0;
        float influence = 0;
        Server.Call(() =>
        {
            var army = Get<Army>(armyId);
            var leader = army.LeaderParty.LeaderHero;
            if (leader.Clan == null) leader.Clan = GameObjectCreator.CreateInitializedObject<Clan>();
            leader.Clan.Influence = 1000f;
            army.Cohesion = 20f;
            cohesion = army.Cohesion;
            influence = leader.Clan.Influence;
            Register("Leader", army.LeaderParty, Outsider);
        });

        Send(Outsider, new NetworkPlayerBoostedArmyCohesion(leaderPartyId, 10f, 5));

        Server.Call(() =>
        {
            var army = Get<Army>(armyId);
            Assert.Equal(cohesion + 10f, army.Cohesion, precision: 2);
            Assert.Equal(influence - 5f, army.LeaderParty.LeaderHero.Clan.Influence, precision: 2);
        });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Disband_IsAppliedOnlyAsTheSendersOwnParty(bool fromMember)
    {
        Server.Call(() =>
        {
            var memberHero = Get<MobileParty>(memberPartyId).LeaderHero;
            if (memberHero.Clan == null) memberHero.Clan = GameObjectCreator.CreateInitializedObject<Clan>();
            // Vanilla's disband cost reads Clan.PlayerClan, which the test campaign leaves unset.
            Campaign.Current.PlayerDefaultFaction = memberHero.Clan;
        });

        Send(fromMember ? Member : Outsider, new NetworkDisbandArmyApplyInternal(armyId, memberPartyId));

        Server.Call(() => Assert.Equal(fromMember, Get<MobileParty>(memberPartyId).Army == null));
    }

    [Fact]
    public void DisbandFromTheKingdomScreenOfAnArmyTheSenderIsNotIn_IsApplied()
    {
        float influence = 0;
        int cost = 0;
        Server.Call(() =>
        {
            var outsider = Get<MobileParty>(outsiderPartyId);
            if (outsider.LeaderHero.Clan == null) outsider.LeaderHero.Clan = GameObjectCreator.CreateInitializedObject<Clan>();
            // Vanilla's kingdom screen only offers a disband for an army the player's party is not in.
            Assert.Null(outsider.Army);
            Campaign.Current.PlayerDefaultFaction = outsider.LeaderHero.Clan;
            outsider.LeaderHero.Clan.Influence = 1000f;
            influence = outsider.LeaderHero.Clan.Influence;
            cost = Campaign.Current.Models.DiplomacyModel.GetInfluenceCostOfDisbandingArmy();
            Assert.True(cost > 0);
        });

        Send(Outsider, new NetworkDisbandArmyApplyInternal(armyId, outsiderPartyId));

        Server.Call(() =>
        {
            Assert.Null(Get<MobileParty>(leaderPartyId).Army);
            Assert.Null(Get<MobileParty>(memberPartyId).Army);
            Assert.Equal(influence - cost, Get<MobileParty>(outsiderPartyId).LeaderHero.Clan.Influence, precision: 2);
        });
    }

    private void Register(string controllerId, MobileParty party, EnvironmentInstance client)
    {
        Assert.True(Server.ObjectManager.TryGetId(party, out var partyId));
        Assert.True(Server.ObjectManager.TryGetId(party.LeaderHero, out var heroId));
        var playerManager = Server.Resolve<IPlayerManager>();
        Assert.True(playerManager.AddPlayer(new Player(controllerId, heroId, partyId, null, null)));
        playerManager.SetPeer(controllerId, client.NetPeer);
    }

    private void Send<T>(EnvironmentInstance sender, T message) where T : Common.Messaging.IMessage
    {
        Server.SimulateMessage(sender.NetPeer, message);
        Server.PumpGameThread();
    }

    private T Get<T>(string id) where T : class => Server.GetRegisteredObject<T>(id);
}
