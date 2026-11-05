using E2E.Tests.Environment.Instance;
using E2E.Tests.Services.Missions;
using E2E.Tests.Util;
using GameInterface.Services.Bandits.Messages;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Bandits;

/// <summary>The server applies bandit encounter results only for the sending player's own party and battle.</summary>
public class BanditRequestSenderTests : MissionTestEnvironment
{
    public BanditRequestSenderTests(ITestOutputHelper output) : base(output, numClients: 3) { }

    private EnvironmentInstance ClientA => Clients.ElementAt(0);
    private EnvironmentInstance ClientB => Clients.ElementAt(1);
    private EnvironmentInstance ClientC => Clients.ElementAt(2);

    [Fact]
    public void BanditSurrenderFromAPlayerOutsideTheBattle_IsRefused()
    {
        var (mapEventId, _) = SetupCoopBattle("ctrl-A", "ctrl-B");
        ConnectPeers();
        RegisterOutsider(ClientC, "ctrl-C");

        Server.SimulateMessage(ClientC.NetPeer, new NetworkBanditsSurrenderAsPrisoners(mapEventId, BattleSideEnum.Attacker));
        Server.PumpGameThread();

        Server.Call(() => Assert.False(Server.GetRegisteredObject<MapEvent>(mapEventId).DefenderSide.IsSurrendered));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void BanditRecruit_IsAppliedOnlyForTheSendersParty(bool fromOwner)
    {
        var (_, partyIds) = SetupCoopBattle("ctrl-A", "ctrl-B");
        ConnectPeers();
        RegisterOutsider(ClientC, "ctrl-C");
        var banditPartyId = CreateParty();

        Server.SimulateMessage((fromOwner ? ClientA : ClientC).NetPeer,
            new NetworkRosterScreenAfterBanditEncounter(new List<string> { banditPartyId }, partyIds[0]));
        Server.PumpGameThread();

        Server.Call(() => Assert.Equal(!fromOwner,
            Server.ObjectManager.TryGetObject<MobileParty>(banditPartyId, out var banditParty) && banditParty.IsActive));
    }

    [Fact]
    public void BanditPrisonerRelease_ForAnotherPlayersParty_IsRefused()
    {
        var (_, partyIds) = SetupCoopBattle("ctrl-A", "ctrl-B");
        ConnectPeers();
        RegisterOutsider(ClientC, "ctrl-C");
        var banditPartyId = CreateParty();
        string clanId = null, prisonerId = null;
        Server.Call(() =>
        {
            var prisonerParty = GameObjectCreator.CreateInitializedObject<MobileParty>();
            var prisoner = prisonerParty.LeaderHero;
            var clan = prisoner.Clan ?? GameObjectCreator.CreateInitializedObject<Clan>();
            prisoner.Clan = clan;
            prisonerParty.RemoveParty();
            TakePrisonerAction.Apply(Server.GetRegisteredObject<MobileParty>(banditPartyId).Party, prisoner);
            Assert.True(Server.ObjectManager.TryGetId(clan, out clanId));
            Assert.True(Server.ObjectManager.TryGetId(prisoner, out prisonerId));
        });

        Server.SimulateMessage(ClientC.NetPeer, new NetworkGetBanditMemberAndPrisonerRosters(
            clanId, partyIds[0], new List<string> { banditPartyId }, doBanditsJoinPlayerSide: false));
        Server.PumpGameThread();

        Server.Call(() => Assert.True(Server.GetRegisteredObject<MobileParty>(banditPartyId).PrisonRoster
            .Contains(Server.GetRegisteredObject<Hero>(prisonerId).CharacterObject)));
    }

    private void ConnectPeers()
    {
        Server.Call(() =>
        {
            var playerManager = Server.Resolve<IPlayerManager>();
            playerManager.SetPeer("ctrl-A", ClientA.NetPeer);
            playerManager.SetPeer("ctrl-B", ClientB.NetPeer);
        });
    }

    // A player with its own party, outside the battle.
    private void RegisterOutsider(EnvironmentInstance client, string controllerId)
    {
        Server.Call(() =>
        {
            var party = GameObjectCreator.CreateInitializedObject<MobileParty>();
            Assert.True(Server.ObjectManager.TryGetId(party, out var partyId));
            Assert.True(Server.ObjectManager.TryGetId(party.LeaderHero, out var heroId));

            var playerManager = Server.Resolve<IPlayerManager>();
            Assert.True(playerManager.AddPlayer(new Player(controllerId, heroId, partyId, null, null)));
            playerManager.SetPeer(controllerId, client.NetPeer);
        });
    }

    private string CreateParty()
    {
        string id = null;
        Server.Call(() => Assert.True(Server.ObjectManager.TryGetId(GameObjectCreator.CreateInitializedObject<MobileParty>(), out id)));
        return id;
    }
}
