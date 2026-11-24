using Common.Messaging;
using Common.Util;
using E2E.Tests.Services.MapEvents;
using E2E.Tests.Util;
using GameInterface.Services.Hideouts;
using GameInterface.Services.MapEvents.Extensions;
using GameInterface.Tests.Utils;
using GameInterface.Services.MapEvents.Handlers;
using GameInterface.Services.MapEvents.Messages.Start;
using GameInterface.Services.MapEvents.TroopSupply;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Services.SiegeEvents.Messages;
using HarmonyLib;
using SandBox.GameComponents;
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
        var battle = CreateServerMapEvent();
        var leaderHeroId = TestEnvironment.CreateRegisteredObject<Hero>();
        RegisterAsPlayerParty("keep-leader", leaderHeroId, battle.AttackerPartyId);
        var joiner = CreatePlayerHeroParty("keep-joiner");
        JoinServerPartyToSide(battle.MapEventId, joiner.partyId, BattleSideEnum.Attacker);
        var escortId = TestEnvironment.CreateRegisteredObject<CharacterObject>();
        var defenderTroopId = TestEnvironment.CreateRegisteredObject<CharacterObject>();
        SeedPartyTroopOnAll(battle.DefenderPartyId, defenderTroopId, DefenderTroops);

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

            Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(escortId, out var escort));
            leaderCharacterId = PreparePlayer("keep-leader", escort, LeaderEscorts);
            joinerCharacterId = PreparePlayer("keep-joiner", escort, JoinerEscorts);
            // The reserves flatten each battle party's own roster, which the seeding above did not refresh.
            foreach (var party in mapEvent.InvolvedParties)
                mapEvent.FindMapEventParty(party)?.Update();
            Server.Resolve<IMessageBroker>().Publish(this, new AuthoritativeSiegeKeepRequested(battle.MapEventId, 1));
        }, disabledMethods);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));
            Assert.Equal(Settlement.SiegeState.InTheLordsHall, settlement.CurrentSiegeState);
        });
        var choosing = Server.NetworkSentMessages.GetMessages<NetworkSiegeKeepPhaseChanged>()
            .Where(message => message.Phase == SiegeKeepPhase.Choosing).ToArray();
        Assert.Equal(2, choosing.Length);
        Assert.All(choosing, message => Assert.True(message.IsSeated));
        // 30 defenders put 27 in the hall, so 19 attacker places: 2 heroes and the 13 escorts both parties have.
        Assert.Equal(new[] { JoinerEscorts, LeaderEscorts }, choosing.Select(message => message.EscortAllowance).OrderBy(x => x));

        Server.SimulateMessage(clients[1].NetPeer, new NetworkSiegeKeepEntryRequest("joiner-overfull", battle.MapEventId,
            false, new[] { new HideoutTroopSelectionEntry(escortId, JoinerEscorts + 1) }));
        Assert.False(Reply("joiner-overfull").Accepted);

        Server.SimulateMessage(clients[0].NetPeer, new NetworkSiegeKeepEntryRequest("leader-pick", battle.MapEventId,
            false, new[] { new HideoutTroopSelectionEntry(leaderCharacterId, 1), new HideoutTroopSelectionEntry(escortId, 5) }));
        var leaderReply = Reply("leader-pick");
        Assert.True(leaderReply.Accepted, leaderReply.Reason);
        Assert.False(leaderReply.IsLocked);

        Server.SimulateMessage(clients[1].NetPeer, new NetworkSiegeKeepEntryRequest("joiner-pick", battle.MapEventId,
            false, new[] { new HideoutTroopSelectionEntry(escortId, 2) }));
        var joinerReply = Reply("joiner-pick");
        Assert.True(joinerReply.Accepted, joinerReply.Reason);
        Assert.True(joinerReply.IsLocked);
        Assert.Equal(2, Server.NetworkSentMessages.GetMessages<NetworkSiegeKeepPhaseChanged>()
            .Count(message => message.Phase == SiegeKeepPhase.Locked && message.IsSeated));

        var spawnSettings = disabledMethods
            .Append(AccessTools.PropertyGetter(typeof(MBGameManager), nameof(MBGameManager.UnitSpawnPrioritization)))
            .ToList();
        Server.Call(() => Server.SimulateMessage(clients[0].NetPeer, new NetworkBattleStartRequest("keep-start",
            (int)BattleStartMode.Mission, battle.MapEventId, battle.AttackerPartyId)), spawnSettings);
        Assert.True(Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkBattleStartReply>(),
            reply => reply.RequestId == "keep-start").Accepted);
        var starts = Server.NetworkSentMessages.GetMessages<NetworkStartSiegeMission>().ToArray();
        Assert.Equal(2, starts.Length);
        Assert.All(starts, start => Assert.True(start.IsLordsHall));

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(battle.MapEventId, out var mapEvent));
            var reserves = Server.Resolve<IBattleTroopReserveBuilder>();
            Assert.Equal(6, OwnedEntries(reserves.GetOwnedReserves(mapEvent, "keep-leader", true), BattleSideEnum.Attacker));
            Assert.Equal(3, OwnedEntries(reserves.GetOwnedReserves(mapEvent, "keep-joiner", false), BattleSideEnum.Attacker));
            Assert.Equal(27, OwnedEntries(reserves.GetOwnedReserves(mapEvent, "keep-leader", true), BattleSideEnum.Defender));
            Assert.Contains(joinerCharacterId, reserves.GetOwnedReserves(mapEvent, "keep-joiner", false)
                .Single(side => side.Side == BattleSideEnum.Attacker).Parties.Single().Entries
                .Select(entry => entry.CharacterId));
        }, disabledMethods);
    }

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
    private string PreparePlayer(string controllerId, CharacterObject escort, int escorts)
    {
        var players = Server.Resolve<IPlayerManager>();
        Assert.True(players.TryGetPlayer(controllerId, out var player));
        Assert.True(Server.ObjectManager.TryGetObject<Hero>(player.HeroId, out var hero));
        Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var party));
        Assert.True(Server.ObjectManager.TryGetId(hero.CharacterObject, out var characterId));
        Assert.True(players.ReplacePlayer(player,
            new Player(player.ControllerId, player.HeroId, player.MobilePartyId, player.ClanId, characterId)));
        hero.HitPoints = 100;
        party.IsActive = true;
        party.MemberRoster.Clear();
        party.MemberRoster.AddToCounts(hero.CharacterObject, 1);
        party.MemberRoster.AddToCounts(escort, escorts);
        return characterId;
    }
}
