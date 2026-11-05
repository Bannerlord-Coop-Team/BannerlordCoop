using Coop.Core.Client.Services.SiegeEvents.Messages;
using Coop.Core.Server.Services.SiegeEvents.Messages;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.Armies.Messages;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.MapEvents.Messages.Leave;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Services.SiegeEvents.Patches;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

/// <summary>The server applies battle, siege and army requests only for the sending player's own party or clan.</summary>
public class BattleRequestSenderTests : MissionTestEnvironment
{
    public BattleRequestSenderTests(ITestOutputHelper output) : base(output, numClients: 3) { }

    private EnvironmentInstance ClientA => Clients.ElementAt(0);
    private EnvironmentInstance ClientB => Clients.ElementAt(1);
    private EnvironmentInstance ClientC => Clients.ElementAt(2);

    [Fact]
    public void LeaveBattleForAnotherPlayersParty_IsRefused()
    {
        var (mapEventId, partyIds) = SetupCoopBattle("ctrl-A", "ctrl-B");
        ConnectPeers();
        string partyBaseAId = PartyBaseId(partyIds[0]);

        Server.SimulateMessage(ClientB.NetPeer, new NetworkRequestLeaveBattle(partyBaseAId));
        Server.PumpGameThread();

        Server.Call(() => Assert.Same(
            Server.GetRegisteredObject<MapEvent>(mapEventId), Server.GetRegisteredObject<MobileParty>(partyIds[0]).MapEvent));
    }

    [Fact]
    public void SurrenderFromAPlayerOutsideTheBattle_IsRefused()
    {
        var (mapEventId, _) = SetupCoopBattle("ctrl-A", "ctrl-B");
        ConnectPeers();
        RegisterOutsider(ClientC, "ctrl-C");

        Server.SimulateMessage(ClientC.NetPeer, new NetworkMapEventSurrender(mapEventId, BattleSideEnum.Defender));
        Server.PumpGameThread();

        Server.Call(() =>
        {
            var mapEvent = Server.GetRegisteredObject<MapEvent>(mapEventId);
            Assert.False(mapEvent.DefenderSide.IsSurrendered);
            Assert.Equal(BattleState.None, mapEvent.BattleState);
        });
    }

    [Fact]
    public void FinalizeFromAnUnregisteredPeer_IsRefusedWhileAHostIsElected()
    {
        var (mapEventId, _) = SetupCoopBattle("ctrl-A", "ctrl-B");
        ConnectPeers();
        EnterBattle(ClientA, mapEventId);
        AssertHost(Server, mapEventId, "ctrl-A");

        Server.SimulateMessage(ClientC.NetPeer, new NetworkMapEventFinalizeAttempted(mapEventId));
        Server.PumpGameThread();

        Server.Call(() => Assert.False(Server.GetRegisteredObject<MapEvent>(mapEventId).IsFinalized));
    }

    [Fact]
    public void BreakSiegeForAnotherPlayersParty_IsRefused()
    {
        string ownerPartyId = RegisterOutsider(ClientA, "ctrl-A");
        RegisterOutsider(ClientB, "ctrl-B");
        Server.NetworkSentMessages.Clear();

        Server.SimulateMessage(ClientB.NetPeer, new NetworkRequestBreakSiege(ownerPartyId));
        Server.PumpGameThread();

        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkBreakSiegeApproved>());
    }

    // An applied aftermath choice takes the settlement's pending aftermath, so a refused one leaves it pending.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SiegeAftermathChoice_IsAppliedOnlyForTheSendersParty(bool fromOwner)
    {
        string ownerPartyId = RegisterOutsider(ClientA, "ctrl-A");
        RegisterOutsider(ClientB, "ctrl-B");
        string settlementId = null;
        Settlement settlement = null;
        Server.Call(() =>
        {
            settlement = GameObjectCreator.CreateInitializedObject<Settlement>();
            var town = GameObjectCreator.CreateInitializedObject<Town>();
            var captor = GameObjectCreator.CreateInitializedObject<Clan>();
            settlement.Town = town;
            settlement.SetSettlementComponent(town);
            town.OwnerClan = captor;
            town.IsOwnerUnassigned = false;
            town.LastCapturedBy = captor;

            var pending = new SiegeAftermathPatches.PendingAftermath(
                Server.GetRegisteredObject<MobileParty>(ownerPartyId), GameObjectCreator.CreateInitializedObject<Clan>(), new Dictionary<MobileParty, float>());
            Assert.True(pending.TryRestoreCaptureBinding(captor, captor));
            Assert.True(pending.MatchesCurrentCapture(settlement));
            SiegeAftermathPatches.PendingAftermaths[settlement] = pending;
            Assert.True(Server.ObjectManager.TryGetId(settlement, out settlementId));
        });

        try
        {
            Server.SimulateMessage((fromOwner ? ClientA : ClientB).NetPeer, new NetworkRequestSiegeAftermath(ownerPartyId, settlementId, 0));
            Server.PumpGameThread();

            Assert.Equal(!fromOwner, SiegeAftermathPatches.PendingAftermaths.ContainsKey(settlement));
        }
        finally
        {
            SiegeAftermathPatches.PendingAftermaths.TryRemove(settlement, out _);
        }
    }

    [Theory]
    [InlineData("other", 50, false)]
    [InlineData("own", -50, false)]
    [InlineData("own", 50, true)]
    public void InfluenceSpend_AppliesOnlyToTheSendersOwnClanAndOnlyAsASpend(string sender, int influence, bool applied)
    {
        string ownerPartyId = RegisterOutsider(ClientA, "ctrl-A");
        RegisterOutsider(ClientB, "ctrl-B");
        string clanId = null;
        Server.Call(() =>
        {
            var clan = Server.GetRegisteredObject<MobileParty>(ownerPartyId).LeaderHero.Clan;
            clan.Influence = 100f;
            Assert.True(Server.ObjectManager.TryGetId(clan, out clanId));
        });

        Server.SimulateMessage((sender == "own" ? ClientA : ClientB).NetPeer, new NetworkChangeClanInfluence(clanId, influence));
        Server.PumpGameThread();

        Server.Call(() => Assert.Equal(applied ? 50f : 100f, Server.GetRegisteredObject<Clan>(clanId).Influence, precision: 2));
    }

    // Links the battle's first two players to their clients' peers, as a real join does.
    private void ConnectPeers()
    {
        Server.Call(() =>
        {
            var playerManager = Server.Resolve<IPlayerManager>();
            playerManager.SetPeer("ctrl-A", ClientA.NetPeer);
            playerManager.SetPeer("ctrl-B", ClientB.NetPeer);
        });
    }

    // A player with its own party and clan, outside any battle.
    private string RegisterOutsider(EnvironmentInstance client, string controllerId)
    {
        string partyId = null;
        Server.Call(() =>
        {
            var party = GameObjectCreator.CreateInitializedObject<MobileParty>();
            Assert.True(Server.ObjectManager.TryGetId(party, out partyId));
            Assert.True(Server.ObjectManager.TryGetId(party.LeaderHero, out var heroId));

            var playerManager = Server.Resolve<IPlayerManager>();
            Assert.True(playerManager.AddPlayer(new Player(controllerId, heroId, partyId, null, null)));
            playerManager.SetPeer(controllerId, client.NetPeer);
        });
        return partyId;
    }

    private string PartyBaseId(string mobilePartyId)
    {
        string id = null;
        Server.Call(() => Assert.True(Server.ObjectManager.TryGetId(Server.GetRegisteredObject<MobileParty>(mobilePartyId).Party, out id)));
        return id;
    }
}
