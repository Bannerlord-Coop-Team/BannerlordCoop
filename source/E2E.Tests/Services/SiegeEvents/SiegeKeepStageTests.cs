using Common.Messaging;
using Common.Util;
using E2E.Tests.Services.MapEvents;
using E2E.Tests.Util;
using GameInterface.Services.Hideouts;
using GameInterface.Services.MapEvents.Extensions;
using GameInterface.Tests.Utils;
using GameInterface.Services.MapEvents.Handlers;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.MapEvents.Messages.Start;
using GameInterface.Services.MapEvents.TroopSupply;
using GameInterface.Services.PlayerCaptivityService.Messages;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Services.SiegeEvents;
using GameInterface.Services.SiegeEvents.Messages;
using HarmonyLib;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using TaleWorlds.CampaignSystem.Settlements.Locations;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using Xunit.Abstractions;

namespace E2E.Tests.Services.SiegeEvents;

/// <summary>
/// The keep stage after the walls of a player-led siege assault: both walls players get a place within vanilla's hall
/// limits, choose their squads, and the keep opens for both with only those squads and vanilla's hall defenders.
/// </summary>
public class SiegeKeepStageTests : MapEventTestBase
{
    private const int DefenderTroops = 30;
    private const int LeaderEscorts = 10;
    private const int JoinerEscorts = 3;

    public SiegeKeepStageTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void WallsPullBack_SeatsBothPlayersAndStartsTheKeepWithTheirSquads()
    {
        // Every real keep follows a walls mission on the same map event, whose start snapshot it must not reuse.
        var keep = OpenKeepStage(TimeSpan.FromSeconds(90), DefenderTroops, startWallsFirst: true);
        var clients = Clients.ToArray();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(keep.SettlementId, out var settlement));
            Assert.Equal(Settlement.SiegeState.InTheLordsHall, settlement.CurrentSiegeState);
        });
        var choosing = Server.NetworkSentMessages.GetMessages<NetworkSiegeKeepPhaseChanged>()
            .Where(message => message.Phase == SiegeKeepPhase.Choosing).ToArray();
        Assert.Equal(2, choosing.Length);
        Assert.All(choosing, message => Assert.True(message.IsSeated));
        // 30 defenders put 27 in the hall, so 19 attacker places: 2 heroes and the 13 escorts both parties have.
        Assert.Equal(new[] { JoinerEscorts, LeaderEscorts }, choosing.Select(message => message.EscortAllowance).OrderBy(x => x));

        Server.SimulateMessage(clients[0].NetPeer, new NetworkSiegeKeepEntryRequest("leader-pick", keep.Battle.MapEventId,
            false, new[] { new HideoutTroopSelectionEntry(keep.LeaderCharacterId, 1), new HideoutTroopSelectionEntry(keep.EscortId, 5) }));
        var leaderReply = Reply("leader-pick");
        Assert.True(leaderReply.Accepted, leaderReply.Reason);
        Assert.False(leaderReply.IsLocked);

        Server.SimulateMessage(clients[1].NetPeer, new NetworkSiegeKeepEntryRequest("joiner-pick", keep.Battle.MapEventId,
            false, new[] { new HideoutTroopSelectionEntry(keep.EscortId, 2) }));
        var joinerReply = Reply("joiner-pick");
        Assert.True(joinerReply.Accepted, joinerReply.Reason);
        Assert.True(joinerReply.IsLocked);
        Assert.Equal(2, LockedSeats());

        StartKeepMission(keep, expectedStarts: 2);
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(keep.Battle.MapEventId, out var mapEvent));
            var reserves = Server.Resolve<IBattleTroopReserveBuilder>();
            Assert.Equal(6, OwnedEntries(reserves.GetOwnedReserves(mapEvent, "keep-leader", true), BattleSideEnum.Attacker));
            Assert.Equal(3, OwnedEntries(reserves.GetOwnedReserves(mapEvent, "keep-joiner", false), BattleSideEnum.Attacker));
            Assert.Equal(27, OwnedEntries(reserves.GetOwnedReserves(mapEvent, "keep-leader", true), BattleSideEnum.Defender));
            Assert.Contains(keep.JoinerCharacterId, reserves.GetOwnedReserves(mapEvent, "keep-joiner", false)
                .Single(side => side.Side == BattleSideEnum.Attacker).Parties.Single().Entries
                .Select(entry => entry.CharacterId));
        }, keep.DisabledMethods);
    }

    // Players who never answer before the selection window ends bring vanilla's suggested squad, sized to the escort
    // places their seat got: 10 defenders give 7 places, so 5 escorts shared 3 and 2.
    [Fact]
    public void SelectionDeadline_LocksTheRosterWithSuggestedSquadsWithinEachShare()
    {
        var keep = OpenKeepStage(TimeSpan.Zero, defenders: 10);

        Server.Call(() => Server.Resolve<IMessageBroker>().Publish(this, new CampaignTick()));

        Assert.Equal(2, LockedSeats());
        StartKeepMission(keep, expectedStarts: 2);
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(keep.Battle.MapEventId, out var mapEvent));
            var reserves = Server.Resolve<IBattleTroopReserveBuilder>();
            Assert.Equal(4, OwnedEntries(reserves.GetOwnedReserves(mapEvent, "keep-leader", true), BattleSideEnum.Attacker));
            Assert.Equal(3, OwnedEntries(reserves.GetOwnedReserves(mapEvent, "keep-joiner", false), BattleSideEnum.Attacker));
        }, keep.DisabledMethods);
    }

    // With every walls player's hero wounded nobody can lead the keep, so the walls end the way an AI-led assault does.
    [Fact]
    public void NoHealthyAttackingPlayer_ConcludesTheWallsAsAnAttackerVictory()
    {
        var conclusions = new List<AuthoritativeBattleConclusionRequested>();
        Server.Call(() => Server.Resolve<IMessageBroker>().Subscribe<AuthoritativeBattleConclusionRequested>(
            payload => conclusions.Add(payload.What)));

        var keep = OpenKeepStage(TimeSpan.FromSeconds(90), DefenderTroops, woundHeroes: true);

        Assert.Equal(BattleState.AttackerVictory, Assert.Single(conclusions).BattleState);
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkSiegeKeepPhaseChanged>());
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(keep.SettlementId, out var settlement));
            Assert.Equal(Settlement.SiegeState.OnTheWalls, settlement.CurrentSiegeState);
        });
    }

    // A single defender leaves one attacker place, so the siege leader goes in alone and the joiner watches.
    [Fact]
    public void SingleHallPlace_SeatsOnlyTheSiegeLeader()
    {
        var keep = OpenKeepStage(TimeSpan.FromSeconds(90), defenders: 1);
        var clients = Clients.ToArray();

        var choosing = Server.NetworkSentMessages.GetMessages<NetworkSiegeKeepPhaseChanged>()
            .Where(message => message.Phase == SiegeKeepPhase.Choosing).ToArray();
        Assert.Single(choosing, message => message.IsSeated);
        Assert.Single(choosing, message => !message.IsSeated);

        Server.SimulateMessage(clients[0].NetPeer, new NetworkSiegeKeepEntryRequest("leader-alone", keep.Battle.MapEventId,
            false, Array.Empty<HideoutTroopSelectionEntry>()));
        Assert.True(Reply("leader-alone").IsLocked);
        StartKeepMission(keep, expectedStarts: 1);
    }

    private sealed record KeepStage(MapEventContext Battle, string SettlementId, string EscortId,
        string LeaderCharacterId, string JoinerCharacterId, List<MethodBase> DisabledMethods);

    // Two attacking players at a besieged town whose walls just fell back, with the keep stage opened by the server.
    private KeepStage OpenKeepStage(TimeSpan selectionWindow, int defenders, bool startWallsFirst = false,
        bool woundHeroes = false)
    {
        var battle = CreateServerMapEvent();
        var leaderHeroId = TestEnvironment.CreateRegisteredObject<Hero>();
        RegisterAsPlayerParty("keep-leader", leaderHeroId, battle.AttackerPartyId);
        var joiner = CreatePlayerHeroParty("keep-joiner");
        JoinServerPartyToSide(battle.MapEventId, joiner.partyId, BattleSideEnum.Attacker);
        var escortId = TestEnvironment.CreateRegisteredObject<CharacterObject>();
        var defenderTroopId = TestEnvironment.CreateRegisteredObject<CharacterObject>();

        var clients = Clients.ToArray();
        TestEnvironment.ConnectRegisteredPlayer(clients[0], "keep-leader");
        TestEnvironment.ConnectRegisteredPlayer(clients[1], "keep-joiner");
        string settlementId = null!, leaderCharacterId = null!, joinerCharacterId = null!;
        var disabledMethods = MapEventDisabledMethods.Concat(SiegeCreationDisabledMethods).ToList();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(battle.MapEventId, out var mapEvent));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(battle.AttackerPartyId, out var leaderParty));
            var settlement = CreateBesiegedSettlement(leaderParty);
            Assert.True(Server.ObjectManager.TryGetId(settlement, out settlementId));
            mapEvent.SetBattleType(MapEvent.BattleTypes.Siege);
            mapEvent.SetMapEventSettlement(settlement);
            EnsureLordsHallModel();

            // The fixture party comes with troops of its own, so the hall sees exactly these defenders.
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(battle.DefenderPartyId, out var defenderParty));
            Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(defenderTroopId, out var defenderTroop));
            defenderParty.MemberRoster.Clear();
            defenderParty.MemberRoster.AddToCounts(defenderTroop, defenders);

            Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(escortId, out var escort));
            leaderCharacterId = PreparePlayer("keep-leader", escort, LeaderEscorts, woundHeroes);
            joinerCharacterId = PreparePlayer("keep-joiner", escort, JoinerEscorts, woundHeroes);
            // The reserves flatten each battle party's own roster, which the seeding above did not refresh.
            foreach (var party in mapEvent.InvolvedParties)
                mapEvent.FindMapEventParty(party)?.Update();
        }, disabledMethods);

        var keep = new KeepStage(battle, settlementId, escortId, leaderCharacterId, joinerCharacterId, disabledMethods);
        if (startWallsFirst)
        {
            StartMission(keep, "walls-start");
            Assert.All(Server.NetworkSentMessages.GetMessages<NetworkStartSiegeMission>(),
                start => Assert.False(start.IsLordsHall));
        }

        Server.Call(() =>
        {
            ((SiegeKeepStage)Server.Resolve<ISiegeKeepStage>()).SelectionWindow = selectionWindow;
            Server.Resolve<IMessageBroker>().Publish(this, new AuthoritativeSiegeKeepRequested(battle.MapEventId, 1));
        }, disabledMethods);
        return keep;
    }

    // The leader asks for the keep mission, which the server opens for every seated player.
    private void StartKeepMission(KeepStage keep, int expectedStarts)
    {
        StartMission(keep, "keep-start");
        Assert.Equal(expectedStarts, Server.NetworkSentMessages.GetMessages<NetworkStartSiegeMission>()
            .Count(start => start.IsLordsHall));
    }

    private void StartMission(KeepStage keep, string requestId)
    {
        var spawnSettings = keep.DisabledMethods
            .Append(AccessTools.PropertyGetter(typeof(MBGameManager), nameof(MBGameManager.UnitSpawnPrioritization)))
            .ToList();
        Server.Call(() => Server.SimulateMessage(Clients.First().NetPeer, new NetworkBattleStartRequest(requestId,
            (int)BattleStartMode.Mission, keep.Battle.MapEventId, keep.Battle.AttackerPartyId)), spawnSettings);
        Assert.True(Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkBattleStartReply>(),
            reply => reply.RequestId == requestId).Accepted);
    }

    private int LockedSeats() => Server.NetworkSentMessages.GetMessages<NetworkSiegeKeepPhaseChanged>()
        .Count(message => message.Phase == SiegeKeepPhase.Locked && message.IsSeated);

    private NetworkSiegeKeepEntryReply Reply(string requestId) =>
        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkSiegeKeepEntryReply>(),
            reply => reply.RequestId == requestId);

    private static int OwnedEntries(IReadOnlyList<SideReserve> reserves, BattleSideEnum side) =>
        reserves.Single(reserve => reserve.Side == side).Parties.Sum(party => party.Entries.Length);

    // A besieged town with a lord's hall, as the keep reads it.
    private static Settlement CreateBesiegedSettlement(MobileParty besieger)
    {
        var settlement = GameObjectCreator.CreateInitializedObject<Settlement>();
        using (new AllowedThread())
        {
            CreateReplicableSiegeEvent(settlement, besieger);
            settlement.Town = ObjectHelper.SkipConstructor<Town>();
            settlement.Town.Buildings = new MBList<Building>();
            settlement.LocationComplex = new LocationComplex();
            settlement.LocationComplex._locations.Add("lordshall", new Location(
                "lordshall", new TextObject("Lord's hall"), new TextObject("Lord's hall"), 0, true, false,
                "CanAlways", "CanAlways", "CanAlways", "CanAlways",
                new[] { "e2e_hall", "e2e_hall", "e2e_hall", "e2e_hall" }, settlement.LocationComplex));
        }
        return settlement;
    }

    // The harness campaign has no hall model, which the keep reads for vanilla's limits.
    private static void EnsureLordsHallModel()
    {
        if (Campaign.Current.Models?.SiegeLordsHallFightModel != null) return;

        var models = Campaign.Current.Models?.GetGameModels().ToList() ?? new List<GameModel>();
        models.Add(new DefaultSiegeLordsHallFightModel());
        Campaign.Current._gameModels = new GameModels(models);
    }

    // Gives the player its real hero character id, a healthy hero, and escorts in its party.
    private string PreparePlayer(string controllerId, CharacterObject escort, int escorts, bool wounded)
    {
        var players = Server.Resolve<IPlayerManager>();
        Assert.True(players.TryGetPlayer(controllerId, out var player));
        Assert.True(Server.ObjectManager.TryGetObject<Hero>(player.HeroId, out var hero));
        Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var party));
        Assert.True(Server.ObjectManager.TryGetId(hero.CharacterObject, out var characterId));
        Assert.True(players.ReplacePlayer(player,
            new Player(player.ControllerId, player.HeroId, player.MobilePartyId, player.ClanId, characterId)));
        hero.HitPoints = wounded ? 1 : 100;
        party.IsActive = true;
        party.MemberRoster.Clear();
        party.MemberRoster.AddToCounts(hero.CharacterObject, 1);
        party.MemberRoster.AddToCounts(escort, escorts);
        return characterId;
    }
}
