using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.Actions.Messages;
using GameInterface.Services.Armies.Messages;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using HarmonyLib;
using SandBox.GauntletUI.Map;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Armies;

/// <summary>The server applies army, disband and influence requests only for the sending player's own party, clan or army.</summary>
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

    [Fact]
    public void AddingAnotherPlayersPartyToTheArmy_IsRefused()
    {
        var partyId = environment.CreateRegisteredObject<MobileParty>();

        Send(Outsider, new NetworkAddMobilePartyInArmy(armyId, partyId, false));

        Server.Call(() => Assert.Null(Get<MobileParty>(partyId).Army));
    }

    [Fact]
    public void CohesionBoostFromAPlayerWhoDoesNotLeadTheArmy_IsRefused()
    {
        Server.Call(() =>
        {
            var army = Get<Army>(armyId);
            var leader = army.LeaderParty.LeaderHero;
            if (leader.Clan == null) leader.Clan = GameObjectCreator.CreateInitializedObject<Clan>();
            leader.Clan.Influence = 1000f;
            army.Cohesion = 20f;
        });

        Send(Outsider, new NetworkPlayerBoostedArmyCohesion(leaderPartyId, 10f, 0));

        Server.Call(() => Assert.Equal(20f, Get<Army>(armyId).Cohesion));
    }

    // The kingdom screen disbands an army the sender's party is not in, paid from the sender's own clan.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Disband_IsAppliedOnlyAsTheSendersOwnParty(bool ownParty)
    {
        Server.Call(() =>
        {
            foreach (var party in new[] { Get<MobileParty>(outsiderPartyId), Get<MobileParty>(memberPartyId) })
                if (party.LeaderHero.Clan == null) party.LeaderHero.Clan = GameObjectCreator.CreateInitializedObject<Clan>();
            // Vanilla's disband cost reads Clan.PlayerClan, which the test campaign leaves unset.
            Campaign.Current.PlayerDefaultFaction = Get<MobileParty>(outsiderPartyId).LeaderHero.Clan;
        });

        Send(Outsider, new NetworkDisbandArmyApplyInternal(armyId, ownParty ? outsiderPartyId : memberPartyId));

        Server.Call(() => Assert.Equal(ownParty, Get<MobileParty>(memberPartyId).Army == null));
    }

    [Theory]
    [InlineData(false, 50, false)]
    [InlineData(true, -50, false)]
    [InlineData(true, 50, true)]
    public void InfluenceSpend_AppliesOnlyToTheSendersOwnClanAndOnlyAsASpend(bool ownClan, int influence, bool applied)
    {
        string clanId = null;
        Server.Call(() =>
        {
            var clan = Get<MobileParty>(outsiderPartyId).LeaderHero.Clan ??= GameObjectCreator.CreateInitializedObject<Clan>();
            clan.Influence = 100f;
            Assert.True(Server.ObjectManager.TryGetId(clan, out clanId));
        });

        Send(ownClan ? Outsider : Member, new NetworkChangeClanInfluence(clanId, influence));

        Server.Call(() => Assert.Equal(applied ? 50f : 100f, Get<Clan>(clanId).Influence, precision: 2));
    }

    [Fact]
    public void DisbandFromTheKingdomScreenOfAnArmyInABattle_IsIgnored()
    {
        float influence = 0;
        Server.Call(() =>
        {
            var outsider = Get<MobileParty>(outsiderPartyId);
            if (outsider.LeaderHero.Clan == null) outsider.LeaderHero.Clan = GameObjectCreator.CreateInitializedObject<Clan>();
            Campaign.Current.PlayerDefaultFaction = outsider.LeaderHero.Clan;
            outsider.LeaderHero.Clan.Influence = 1000f;
            influence = outsider.LeaderHero.Clan.Influence;

            // A stale kingdom screen can still send the disband after the army's leader joined a battle.
            var mapEvent = GameObjectCreator.CreateInitializedObject<MapEvent>();
            mapEvent.MapEventVisual = ObjectHelper.SkipConstructor<GauntletMapEventVisual>();
            mapEvent.Initialize(
                GameObjectCreator.CreateInitializedObject<MobileParty>().Party,
                GameObjectCreator.CreateInitializedObject<MobileParty>().Party,
                new FieldBattleEventComponent(mapEvent),
                MapEvent.BattleTypes.FieldBattle);
            mapEvent.MapEventVisual = null;
            Get<MobileParty>(leaderPartyId).Party.MapEventSide = mapEvent.AttackerSide;
            Assert.NotNull(Get<MobileParty>(leaderPartyId).MapEvent);
        }, new[] { AccessTools.Method(typeof(GauntletMapEventVisual), nameof(GauntletMapEventVisual.Initialize)) });

        Send(Outsider, new NetworkDisbandArmyApplyInternal(armyId, outsiderPartyId));

        Server.Call(() =>
        {
            var army = Get<Army>(armyId);
            Assert.Same(army, Get<MobileParty>(leaderPartyId).Army);
            Assert.Same(army, Get<MobileParty>(memberPartyId).Army);
            Assert.Contains(Get<MobileParty>(memberPartyId), army.Parties);
            Assert.Equal(influence, Get<MobileParty>(outsiderPartyId).LeaderHero.Clan.Influence, precision: 2);
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
