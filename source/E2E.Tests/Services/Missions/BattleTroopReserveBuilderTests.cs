using Common.Messaging;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.MapEvents.TroopSupply;
using GameInterface.Services.MapEvents.TroopSupply.Messages;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

/// <summary>Tests the authoritative troop reserves supplied to coop battle owners.</summary>
public class BattleTroopReserveBuilderTests : MissionTestEnvironment
{
    public BattleTroopReserveBuilderTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void GetOwnedReserves_ExcludesTroopsThatCannotJoinBattle()
    {
        var (mapEventId, _) = SetupCoopBattle("attacker", "defender");

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventId, out var mapEvent));
            var party = mapEvent.DefenderSide.Parties[0];
            var troop = Server.CreateRegisteredObject<CharacterObject>("reserve_eligibility_troop");
            Assert.True(Server.ObjectManager.TryGetId(troop, out var troopId));

            party.Party.MemberRoster.Clear();
            party.Party.MemberRoster.AddToCounts(troop, 4, woundedCount: 1);
            party.Update();

            var battleReady = party.Troops
                .Where(element => !element.IsWounded && !element.IsRouted && !element.IsKilled)
                .ToArray();
            Assert.Equal(3, battleReady.Length);

            party.OnTroopKilled(battleReady[0].Descriptor);
            party.OnTroopRouted(battleReady[1].Descriptor);

            var reserves = Server.Resolve<IBattleTroopReserveBuilder>()
                .GetOwnedReserves(mapEvent, "defender", isHost: false);
            var defenderReserve = reserves.Single(reserve => reserve.Side == BattleSideEnum.Defender);
            var partyReserve = Assert.Single(defenderReserve.Parties);
            var entry = Assert.Single(partyReserve.Entries);

            Assert.Equal(troopId, entry.CharacterId);
        });
    }

    [Fact]
    public void GetOwnedReserves_PlayerHeroIsFirstAndEveryOwnerGetsWholeSideMetadata()
    {
        var (mapEventId, _) = SetupCoopBattle("attacker", "defender");

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventId, out var mapEvent));
            var playerManager = Server.Resolve<IPlayerManager>();
            Assert.True(playerManager.TryGetPlayer("attacker", out var attackerPlayer));
            var attackerHero = Server.CreateRegisteredObject<CharacterObject>("owner_share_player_hero");
            Assert.True(Server.ObjectManager.TryGetId(attackerHero, out var attackerHeroId));
            Assert.True(playerManager.ReplacePlayer(attackerPlayer, new Player(
                attackerPlayer.ControllerId,
                attackerPlayer.HeroId,
                attackerPlayer.MobilePartyId,
                attackerPlayer.ClanId,
                attackerHeroId)));
            var attackerParty = mapEvent.AttackerSide.Parties[0];
            var regular = Server.CreateRegisteredObject<CharacterObject>("owner_share_regular");
            attackerParty.Party.MemberRoster.Clear();
            attackerParty.Party.MemberRoster.AddToCounts(regular, 5);
            attackerParty.Party.MemberRoster.AddToCounts(attackerHero, 1);
            attackerParty.Update();

            var builder = Server.Resolve<IBattleTroopReserveBuilder>();
            var attackerView = builder.GetOwnedReserves(mapEvent, "attacker", isHost: false);
            var defenderView = builder.GetOwnedReserves(mapEvent, "defender", isHost: false);
            var attackerSide = attackerView.Single(reserve => reserve.Side == BattleSideEnum.Attacker);
            var defenderKnowledge = defenderView.Single(reserve => reserve.Side == BattleSideEnum.Attacker);

            var ownedParty = Assert.Single(attackerSide.Parties);
            Assert.Equal(attackerHeroId, ownedParty.Entries[0].CharacterId);
            Assert.Equal(0, ownedParty.PlayerOwnedRank);
            Assert.Equal(6, attackerSide.TotalTroops);
            Assert.Equal(6, defenderKnowledge.TotalTroops);
            Assert.Equal(1, attackerSide.PlayerOwnedPartyCount);
            Assert.Equal(1, defenderKnowledge.PlayerOwnedPartyCount);
        });
    }

    [Fact]
    public void GetOwnedReserves_OfflinePlayerPartyDoesNotReserveAPlayerSlot()
    {
        var (mapEventId, _) = SetupCoopBattle("attacker", "offline-defender");

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventId, out var mapEvent));
            var reserves = Server.Resolve<IBattleTroopReserveBuilder>().GetOwnedReserves(
                mapEvent,
                "attacker",
                isHost: true,
                presentControllers: new HashSet<string> { "attacker" });

            var attacker = reserves.Single(reserve => reserve.Side == BattleSideEnum.Attacker);
            var defender = reserves.Single(reserve => reserve.Side == BattleSideEnum.Defender);

            Assert.Equal(1, attacker.PlayerOwnedPartyCount);
            Assert.Equal(0, defender.PlayerOwnedPartyCount);
            Assert.Equal(-1, Assert.Single(defender.Parties).PlayerOwnedRank);
        });
    }

    [Fact]
    public void GetOwnedReserves_LateAiPartyIsAddedToTheCanonicalHostShare()
    {
        var (mapEventId, _) = SetupCoopBattle("attacker", "defender");
        var latePartyId = CreateRegisteredObject<MobileParty>();
        var lateTroopId = CreateRegisteredObject<CharacterObject>();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventId, out var mapEvent));
            var builder = Server.Resolve<IBattleTroopReserveBuilder>();
            var before = builder.GetOwnedReserves(mapEvent, "attacker", isHost: true);
            var beforeDefenders = before.Single(reserve => reserve.Side == BattleSideEnum.Defender);

            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(latePartyId, out var lateParty));
            Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(lateTroopId, out var lateTroop));
            lateParty.Party.MemberRoster.Clear();
            lateParty.Party.MemberRoster.AddToCounts(lateTroop, 20);
            lateParty.Party.MapEventSide = mapEvent.DefenderSide;

            var after = builder.GetOwnedReserves(mapEvent, "attacker", isHost: true);
            var afterDefenders = after.Single(reserve => reserve.Side == BattleSideEnum.Defender);

            Assert.Equal(beforeDefenders.TotalTroops + 20, afterDefenders.TotalTroops);
            Assert.Equal(beforeDefenders.Parties.Length + 1, afterDefenders.Parties.Length);
            Assert.Equal(20, afterDefenders.Parties.Sum(party => party.Entries.Length));
        });
    }

    [Fact]
    public void LateAiParty_ServerEventRefreshesEveryParticipantAndExpandsTheHost()
    {
        var (mapEventId, _) = SetupCoopBattle("host", "peer");
        var host = Clients.First();
        var peer = Clients.Last();
        Server.Call(() =>
        {
            var players = Server.Resolve<IPlayerManager>();
            players.SetPeer("host", host.NetPeer);
            players.SetPeer("peer", peer.NetPeer);
        });
        EnterBattle(host, mapEventId);
        EnterBattle(peer, mapEventId);

        int hostFeedBaseline = host.InternalMessages.GetMessages<NetworkBattleTroopReserve>()
            .Count(message => message.MapEventId == mapEventId);
        int peerFeedBaseline = peer.InternalMessages.GetMessages<NetworkBattleTroopReserve>()
            .Count(message => message.MapEventId == mapEventId);
        int expansionBaseline = host.InternalMessages.GetMessages<NetworkBattleReserveOwnershipExpanded>()
            .Count(message => message.MapEventId == mapEventId);
        var latePartyId = CreateRegisteredObject<MobileParty>();
        var lateTroopId = CreateRegisteredObject<CharacterObject>();
        string lateMapEventPartyId = null;

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventId, out var mapEvent));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(latePartyId, out var lateParty));
            Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(lateTroopId, out var troop));
            lateParty.Party.MemberRoster.Clear();
            lateParty.Party.MemberRoster.AddToCounts(troop, 20);
            lateParty.Party.MapEventSide = mapEvent.DefenderSide;
            var mapEventParty = mapEvent.DefenderSide.Parties.Last(party => party.Party == lateParty.Party);
            mapEventParty.Update();
            Assert.True(Server.ObjectManager.TryGetId(mapEventParty, out lateMapEventPartyId));

            Server.Resolve<IMessageBroker>().Publish(this,
                new MapEventInvolvedPartiesAdded(mapEvent, new[] { mapEventParty }));
        }, MapEventDisabledMethods);

        var hostFeeds = host.InternalMessages.GetMessages<NetworkBattleTroopReserve>()
            .Where(message => message.MapEventId == mapEventId)
            .Skip(hostFeedBaseline)
            .ToArray();
        var peerFeeds = peer.InternalMessages.GetMessages<NetworkBattleTroopReserve>()
            .Where(message => message.MapEventId == mapEventId)
            .Skip(peerFeedBaseline)
            .ToArray();

        Assert.Equal(2, hostFeeds.Length);
        Assert.Equal(2, peerFeeds.Length);
        Assert.All(hostFeeds.Concat(peerFeeds), message => Assert.True(message.HasAllocationMetadata));
        Assert.Single(hostFeeds.Select(message => message.AllocationRevision).Distinct());
        Assert.Single(peerFeeds.Select(message => message.AllocationRevision).Distinct());
        Assert.Contains(hostFeeds.Single(message => message.Side == (int)BattleSideEnum.Defender).Parties,
            party => party.PartyId == lateMapEventPartyId);
        Assert.Single(host.InternalMessages.GetMessages<NetworkBattleReserveOwnershipExpanded>()
            .Where(message => message.MapEventId == mapEventId)
            .Skip(expansionBaseline));
    }
}
