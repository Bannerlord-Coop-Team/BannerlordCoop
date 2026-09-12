using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.PlayerCaptivityService.Messages;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Services.Tournaments;
using GameInterface.Services.Tournaments.Data;
using GameInterface.Services.Tournaments.Messages;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.TournamentGames;
using TaleWorlds.Core;
using TaleWorlds.Library;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Tournaments;

public class TournamentBettingFlowTests : SyncTestBase
{
    private const string SessionId = "betting-session";
    private const string MissionId = "betting-mission";
    private const string MatchId = "betting-match";
    private const string RoundId = "betting-round";
    private const string BettorControllerId = "tournament-bettor";
    private const string OtherControllerId = "tournament-opponent";
    private const string BettorSlotId = "bettor-slot";
    private const string OpponentSlotId = "opponent-slot";
    private const string BettorTeamId = "bettor-team";
    private const string OpponentTeamId = "opponent-team";
    private const int InitialGold = 1000;

    public TournamentBettingFlowTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public void MultipleBetsAndDuplicateRequest_DeductAndHoldGoldExactlyOnce()
    {
        TournamentFixture fixture = CreateFixture();

        NetworkTournamentBetResult first = PlaceBet(fixture, 40, 1);
        NetworkTournamentBetResult second = PlaceBet(fixture, 60, 2);

        Assert.Equal(100, second.BettedDenars);
        Assert.Equal(100, second.ThisRoundBettedDenars);
        Assert.True(second.ExpectedPayout > first.ExpectedPayout);
        AssertGold(fixture, InitialGold - 100);

        Server.NetworkSentMessages.Clear();
        SendBet(fixture, 60, 2);

        NetworkTournamentBetResult replay = Assert.Single(
            Server.NetworkSentMessages.GetMessages<NetworkTournamentBetResult>());
        Assert.True(replay.Accepted);
        Assert.False(replay.IsSettlement);
        Assert.Equal(second.Sequence, replay.Sequence);
        Assert.Equal(second.BettedDenars, replay.BettedDenars);
        Assert.Equal(second.ThisRoundBettedDenars, replay.ThisRoundBettedDenars);
        Assert.Equal(second.ExpectedPayout, replay.ExpectedPayout);
        AssertGold(fixture, InitialGold - 100);
    }

    [Fact]
    public void LosingLiveMatch_ForfeitsHeldBetWithoutRefund()
    {
        TournamentFixture fixture = CreateFixture();
        PlaceBet(fixture, 100, 1);
        TournamentSessionSnapshot live = StartLiveMatch(fixture);

        Server.NetworkSentMessages.Clear();
        SubmitMatchResult(fixture, live, OpponentTeamId, OpponentSlotId);

        NetworkTournamentBetResult settlement = AssertSettlement("Tournament bet lost");
        Assert.Equal(MatchId, settlement.MatchId);
        AssertGold(fixture, InitialGold - 100);
        Server.Call(() =>
        {
            Assert.True(Server.Resolve<ITournamentSessionRegistry>().TryGet(SessionId, out var completed));
            Assert.True(completed.IsCompleted);
            Assert.Equal(OpponentSlotId, completed.WinnerSlotId);
        });
    }

    [Fact]
    public void WinningTournament_PaysHeldBetExactlyOnce()
    {
        TournamentFixture fixture = CreateFixture();
        NetworkTournamentBetResult accepted = PlaceBet(fixture, 100, 1);
        TournamentSessionSnapshot live = StartLiveMatch(fixture);

        Server.NetworkSentMessages.Clear();
        SubmitMatchResult(fixture, live, BettorTeamId, BettorSlotId);

        AssertSettlement("Tournament bet paid");
        int expectedGold = InitialGold - 100 + accepted.ExpectedPayout;
        AssertGold(fixture, expectedGold);

        Server.NetworkSentMessages.Clear();
        Server.SimulateMessage(this, new CampaignTick());

        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkTournamentBetResult>());
        AssertGold(fixture, expectedGold);
    }

    [Fact]
    public void LeavingDuringLiveMatch_ForfeitsHeldBetWithoutRefund()
    {
        TournamentFixture fixture = CreateFixture(humanOpponent: true);
        PlaceBet(fixture, 100, 1);
        TournamentSessionSnapshot live = StartLiveMatch(fixture);

        Server.NetworkSentMessages.Clear();
        Server.SimulateMessage(
            fixture.Bettor.NetPeer,
            new NetworkRequestLeaveActiveTournament(SessionId, live.Revision));

        NetworkTournamentBetResult settlement = AssertSettlement("Tournament bet forfeited");
        Assert.Equal(MatchId, settlement.MatchId);
        AssertGold(fixture, InitialGold - 100);
        Server.Call(() =>
        {
            Assert.True(Server.Resolve<ITournamentSessionRegistry>().TryGet(SessionId, out var current));
            TournamentContestantData departed = current.Contestants.Single(contestant =>
                contestant.SlotId == BettorSlotId);
            Assert.False(departed.IsHuman);
            Assert.True(departed.IsReplaced);
            Assert.Null(departed.ControllerId);
            Assert.Contains(current.Contestants, contestant =>
                contestant.IsHuman && contestant.ControllerId == OtherControllerId);
        });
    }

    private TournamentFixture CreateFixture(bool humanOpponent = false)
    {
        EnvironmentInstance bettor = Clients.First();
        EnvironmentInstance other = Clients.Skip(1).First();
        string heroId = TestEnvironment.CreateRegisteredObject<Hero>();
        string partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        string townId = TestEnvironment.CreateRegisteredObject<Town>();
        string settlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
        string townPartyId = TestEnvironment.CreateRegisteredObject<PartyBase>();
        string cultureId = TestEnvironment.CreateRegisteredObject<CultureObject>();
        string replacementId = TestEnvironment.CreateRegisteredObject<CharacterObject>();
        string opponentCharacterId = TestEnvironment.CreateRegisteredObject<CharacterObject>();
        string prizeItemId = TestEnvironment.CreateRegisteredObject<ItemObject>();
        string bettorCharacterId = string.Empty;
        TournamentSessionSnapshot snapshot = null!;

        Server.Call(() =>
        {
            BannerManager.Initialize();
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.True(Server.ObjectManager.TryGetObject<Town>(townId, out var town));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));
            Assert.True(Server.ObjectManager.TryGetObject<PartyBase>(townPartyId, out var townParty));
            Assert.True(Server.ObjectManager.TryGetObject<CultureObject>(cultureId, out var culture));
            Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(replacementId, out var replacement));

            hero.Gold = InitialGold;
            hero.PartyBelongedTo = party;
            culture.BasicTroop = replacement;
            settlement.Culture = culture;
            settlement.Town = town;
            townParty.Settlement = settlement;
            town._owner = townParty;

            if (!Server.ObjectManager.TryGetId(hero.CharacterObject, out bettorCharacterId))
            {
                bettorCharacterId = "TournamentBettorCharacter";
                Assert.True(Server.ObjectManager.AddExisting(bettorCharacterId, hero.CharacterObject));
            }

            var playerManager = Server.Resolve<IPlayerManager>();
            Assert.True(playerManager.AddPlayer(new Player(
                BettorControllerId,
                heroId,
                partyId,
                hero.Clan?.StringId ?? string.Empty,
                bettorCharacterId)));
            playerManager.SetPeer(BettorControllerId, bettor.NetPeer);
            if (humanOpponent)
            {
                Assert.True(playerManager.AddPlayer(new Player(
                    OtherControllerId,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    opponentCharacterId)));
                playerManager.SetPeer(OtherControllerId, other.NetPeer);
            }

            snapshot = CreateSnapshot(
                townId,
                prizeItemId,
                bettorCharacterId,
                opponentCharacterId,
                humanOpponent,
                TournamentSessionPhase.AwaitingChoices,
                1);
            Assert.True(Server.Resolve<ITournamentSessionRegistry>().ApplySnapshot(snapshot));
        });

        TestEnvironment.FlushCoalescer();
        Server.NetworkSentMessages.Clear();
        foreach (EnvironmentInstance client in Clients)
        {
            client.NetworkSentMessages.Clear();
            client.InternalMessages.Clear();
        }

        return new TournamentFixture(
            bettor,
            heroId,
            townId,
            prizeItemId,
            bettorCharacterId,
            opponentCharacterId,
            humanOpponent,
            snapshot);
    }

    private NetworkTournamentBetResult PlaceBet(
        TournamentFixture fixture,
        int amount,
        long sequence)
    {
        Server.NetworkSentMessages.Clear();
        SendBet(fixture, amount, sequence);

        NetworkTournamentBetResult result = Assert.Single(
            Server.NetworkSentMessages.GetMessages<NetworkTournamentBetResult>());
        Assert.True(result.Accepted);
        Assert.False(result.IsSettlement);
        Assert.Null(result.Reason);
        Assert.Equal(sequence, result.Sequence);
        Assert.Equal(MatchId, result.MatchId);
        Assert.True(result.ExpectedPayout > 0);
        return result;
    }

    private void SendBet(TournamentFixture fixture, int amount, long sequence)
    {
        BasicCharacterObject previousPlayerTroop = null;
        Server.Call(() => previousPlayerTroop = Game.Current.PlayerTroop);
        Server.SimulateMessage(
            fixture.Bettor.NetPeer,
            new NetworkRequestTournamentBet(
                SessionId,
                fixture.WaitingSnapshot.Revision,
                MatchId,
                amount,
                sequence));
        Server.Call(() => Assert.Same(previousPlayerTroop, Game.Current.PlayerTroop));
    }

    private TournamentSessionSnapshot StartLiveMatch(TournamentFixture fixture)
    {
        TournamentSessionSnapshot live = CreateSnapshot(
            fixture.TownId,
            fixture.PrizeItemId,
            fixture.BettorCharacterId,
            fixture.OpponentCharacterId,
            fixture.HumanOpponent,
            TournamentSessionPhase.LiveMatch,
            fixture.WaitingSnapshot.Revision + 1);
        Server.Call(() =>
        {
            Assert.True(Server.Resolve<ITournamentSessionRegistry>().ApplySnapshot(live));
        });

        TournamentSpawnManifestData manifest = CreateManifest(live);
        Server.NetworkSentMessages.Clear();
        Server.SimulateMessage(
            fixture.Bettor.NetPeer,
            new NetworkSubmitTournamentSpawnManifest(manifest));
        Server.Call(() =>
        {
            Assert.True(Server.Resolve<ITournamentSessionRegistry>().TryGetSpawnManifest(SessionId, out var stored));
            Assert.Equal(manifest.Sequence, stored.Sequence);
            Assert.Equal(2, stored.Agents.Length);
        });
        return live;
    }

    private void SubmitMatchResult(
        TournamentFixture fixture,
        TournamentSessionSnapshot live,
        string winnerTeamId,
        string winnerSlotId)
    {
        string loserTeamId = winnerTeamId == BettorTeamId ? OpponentTeamId : BettorTeamId;
        var result = new TournamentMatchResultData(
            SessionId,
            MatchId,
            live.Revision,
            live.BracketRevision,
            1,
            new[] { winnerTeamId },
            new[] { winnerSlotId },
            new[]
            {
                new TournamentTeamScoreData(winnerTeamId, 1),
                new TournamentTeamScoreData(loserTeamId, 0)
            });

        Server.SimulateMessage(
            fixture.Bettor.NetPeer,
            new NetworkSubmitTournamentMatchResult(result));
    }

    private NetworkTournamentBetResult AssertSettlement(string reason)
    {
        NetworkTournamentBetResult settlement = Assert.Single(
            Server.NetworkSentMessages.GetMessages<NetworkTournamentBetResult>(),
            result => result.IsSettlement);
        Assert.True(settlement.Accepted);
        Assert.Equal(reason, settlement.Reason);
        Assert.Equal(0, settlement.BettedDenars);
        Assert.Equal(0, settlement.ThisRoundBettedDenars);
        Assert.Equal(0, settlement.ExpectedPayout);
        return settlement;
    }

    private void AssertGold(TournamentFixture fixture, int expectedGold)
    {
        TestEnvironment.FlushCoalescer();
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var hero));
            Assert.Equal(expectedGold, hero.Gold);
        });
        fixture.Bettor.Call(() =>
        {
            Assert.True(fixture.Bettor.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var hero));
            Assert.Equal(expectedGold, hero.Gold);
        });
    }

    private static TournamentSessionSnapshot CreateSnapshot(
        string townId,
        string prizeItemId,
        string bettorCharacterId,
        string opponentCharacterId,
        bool humanOpponent,
        TournamentSessionPhase phase,
        long revision)
    {
        TournamentContestantData[] contestants =
        {
            new TournamentContestantData(
                BettorSlotId,
                bettorCharacterId,
                11,
                BettorControllerId,
                "Tournament Bettor",
                true,
                false,
                true,
                opponentCharacterId),
            new TournamentContestantData(
                OpponentSlotId,
                opponentCharacterId,
                22,
                humanOpponent ? OtherControllerId : null,
                "Tournament Opponent",
                humanOpponent,
                false,
                false,
                null)
        };
        var match = new TournamentMatchData(
            MatchId,
            RoundId,
            (int)TournamentMatch.MatchState.Started,
            1,
            1,
            new[]
            {
                new TournamentTeamData(
                    BettorTeamId,
                    new[] { BettorSlotId },
                    0,
                    false,
                    0xFF0000,
                    0x880000,
                    null),
                new TournamentTeamData(
                    OpponentTeamId,
                    new[] { OpponentSlotId },
                    0,
                    false,
                    0x0000FF,
                    0x000088,
                    null)
            },
            Array.Empty<string>());

        return new TournamentSessionSnapshot(
            SessionId,
            MissionId,
            townId,
            "arena",
            prizeItemId,
            phase,
            revision,
            1,
            MatchId,
            BettorControllerId,
            humanOpponent ? new[] { OtherControllerId } : Array.Empty<string>(),
            contestants,
            Array.Empty<string>(),
            Array.Empty<TournamentPlayerChoiceData>(),
            new[] { new TournamentRoundData(RoundId, 0, 0, new[] { match }) },
            0,
            0,
            humanOpponent ? 2 : 1,
            true,
            false,
            null);
    }

    private static TournamentSpawnManifestData CreateManifest(TournamentSessionSnapshot snapshot)
    {
        TournamentMatchData match = Assert.Single(snapshot.Rounds).Matches.Single();
        return new TournamentSpawnManifestData(
            SessionId,
            MatchId,
            snapshot.Revision,
            snapshot.BracketRevision,
            1,
            match.Teams.SelectMany(team => team.ParticipantSlotIds.Select(slotId =>
            {
                TournamentContestantData contestant = snapshot.Contestants.Single(candidate =>
                    candidate.SlotId == slotId);
                return new TournamentAgentSpawnData(
                    Guid.NewGuid(),
                    contestant.SlotId,
                    contestant.CharacterId,
                    contestant.DescriptorSeed,
                    team.TeamId,
                    team.TeamColor,
                    team.TeamColor2,
                    team.BannerCode,
                    contestant.IsHuman ? contestant.ControllerId : snapshot.HostControllerId,
                    Array.Empty<EquipmentElement>(),
                    Vec3.Zero,
                    new Vec2(1f, 0f),
                    100f,
                    Guid.Empty,
                    null,
                    0,
                    Array.Empty<EquipmentElement>(),
                    0f);
            })).ToArray());
    }

    private sealed class TournamentFixture
    {
        public EnvironmentInstance Bettor { get; }
        public string HeroId { get; }
        public string TownId { get; }
        public string PrizeItemId { get; }
        public string BettorCharacterId { get; }
        public string OpponentCharacterId { get; }
        public bool HumanOpponent { get; }
        public TournamentSessionSnapshot WaitingSnapshot { get; }

        public TournamentFixture(
            EnvironmentInstance bettor,
            string heroId,
            string townId,
            string prizeItemId,
            string bettorCharacterId,
            string opponentCharacterId,
            bool humanOpponent,
            TournamentSessionSnapshot waitingSnapshot)
        {
            Bettor = bettor;
            HeroId = heroId;
            TownId = townId;
            PrizeItemId = prizeItemId;
            BettorCharacterId = bettorCharacterId;
            OpponentCharacterId = opponentCharacterId;
            HumanOpponent = humanOpponent;
            WaitingSnapshot = waitingSnapshot;
        }
    }
}
