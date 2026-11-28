using Common.Network;
using Common.Util;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Services.Tournaments;
using GameInterface.Services.Tournaments.Data;
using GameInterface.Services.Tournaments.Messages;
using GameInterface.Services.Tournaments.UI;
using SandBox.Tournaments.MissionLogics;
using System;
using System.Linq;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.TournamentGames;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Tournaments;

public class TournamentBetWindowFlowTests : SyncTestBase
{
    private const string SessionId = "bet-window-session";
    private const string MatchId = "bet-window-match";
    private const string RoundId = "bet-window-round";
    private const string CompetitorControllerId = "bet-window-competitor";
    private const string SpectatorControllerId = "bet-window-spectator";
    private const string CompetitorSlotId = "competitor-slot";
    private const string OpponentSlotId = "opponent-slot";

    public TournamentBetWindowFlowTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public void SpectatorReadyStartsMatch_ClosesCompetitorOpenBetWindow()
    {
        EnvironmentInstance competitor = Clients.First();
        EnvironmentInstance spectator = Clients.Skip(1).First();
        TournamentSessionSnapshot waiting = CreateWaitingSnapshot();
        RegisterSession(competitor, spectator, waiting);
        string prizeId = TestEnvironment.CreateRegisteredObject<ItemObject>();
        string settlementId = TestEnvironment.CreateRegisteredObject<Settlement>();

        CoopTournamentVM viewModel = null!;
        competitor.Call(() =>
        {
            using (new AllowedThread())
                Hero.MainHero.Gold = 1000;
            BannerManager.Initialize();

            Assert.True(competitor.Resolve<ITournamentSessionRegistry>().TryGet(SessionId, out var snapshot));
            viewModel = new CoopTournamentVM(
                () => { },
                CreateTournamentBehavior(competitor, prizeId, settlementId),
                snapshot,
                competitor.Resolve<TournamentUIController>());

            viewModel.ExecuteJoin();
            viewModel.ExecuteOpenBetWindow();
            Assert.True(viewModel.IsBetWindowEnabled);
        });

        spectator.Call(() =>
        {
            Assert.True(spectator.Resolve<ITournamentSessionRegistry>().TryGet(SessionId, out var snapshot));
            spectator.Resolve<TournamentUIController>().RequestChoice(snapshot, TournamentPlayerChoice.Watch);
        });
        competitor.PumpGameThread();

        competitor.Call(() =>
        {
            Assert.True(viewModel.IsCoopMatchActive);
            Assert.False(viewModel.IsBetWindowEnabled);
            viewModel.OnFinalize();
        });
    }

    private void RegisterSession(
        EnvironmentInstance competitor,
        EnvironmentInstance spectator,
        TournamentSessionSnapshot waiting)
    {
        Server.Call(() =>
        {
            var playerManager = Server.Resolve<IPlayerManager>();
            Assert.True(playerManager.AddPlayer(new Player(
                CompetitorControllerId,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty)));
            playerManager.SetPeer(CompetitorControllerId, competitor.NetPeer);
            Assert.True(playerManager.AddPlayer(new Player(
                SpectatorControllerId,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty)));
            playerManager.SetPeer(SpectatorControllerId, spectator.NetPeer);

            Assert.True(Server.Resolve<ITournamentSessionRegistry>().ApplySnapshot(waiting));
            Server.Resolve<INetwork>().SendAll(new NetworkTournamentSessionSnapshot(waiting));
        });

        competitor.Call(() => competitor.Resolve<IControllerIdProvider>().SetControllerId(CompetitorControllerId));
        spectator.Call(() => spectator.Resolve<IControllerIdProvider>().SetControllerId(SpectatorControllerId));
    }

    private static TournamentBehavior CreateTournamentBehavior(
        EnvironmentInstance client,
        string prizeId,
        string settlementId)
    {
        Assert.True(client.ObjectManager.TryGetObject<ItemObject>(prizeId, out var prize));
        Assert.True(client.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));
        prize.Name = new TextObject("Prize");
        var game = (FightTournamentGame)RuntimeHelpers.GetUninitializedObject(typeof(FightTournamentGame));
        game.Prize = prize;

        var behavior = (TournamentBehavior)RuntimeHelpers.GetUninitializedObject(typeof(TournamentBehavior));
        behavior._tournamentGame = game;
        behavior.Settlement = settlement;
        behavior.Rounds = Enumerable.Range(0, 4).Select(_ => CreateRound()).ToArray();
        behavior.CurrentRoundIndex = 0;
        return behavior;
    }

    private static TournamentRound CreateRound()
    {
        var match = new TournamentMatch(1, 1, 1, TournamentGame.QualificationMode.TeamScore);
        match.AddParticipant(new TournamentParticipant(null, new UniqueTroopDescriptor(1)), true);
        // an empty team keeps vanilla from building a portrait row for the placeholder participant
        match.Teams.Single().TeamSize = 0;

        var round = new TournamentRound(1, 1, 1, 1, TournamentGame.QualificationMode.TeamScore);
        round.Matches = new[] { match };
        round.CurrentMatchIndex = 0;
        return round;
    }

    private static TournamentSessionSnapshot CreateWaitingSnapshot()
    {
        TournamentContestantData[] contestants =
        {
            new TournamentContestantData(
                CompetitorSlotId,
                "competitor-character",
                11,
                CompetitorControllerId,
                "Competitor",
                true,
                false,
                true,
                "replacement-character"),
            new TournamentContestantData(
                OpponentSlotId,
                "opponent-character",
                22,
                null,
                "Opponent",
                false,
                false,
                false,
                null)
        };
        var match = new TournamentMatchData(
            MatchId,
            RoundId,
            (int)TournamentMatch.MatchState.Ready,
            1,
            1,
            new[]
            {
                new TournamentTeamData("competitor-team", new[] { CompetitorSlotId }, 0, false, 0xFF0000, 0x880000, null),
                new TournamentTeamData("opponent-team", new[] { OpponentSlotId }, 0, false, 0x0000FF, 0x000088, null)
            },
            Array.Empty<string>());

        return new TournamentSessionSnapshot(
            SessionId,
            "bet-window-mission",
            "bet-window-town",
            "arena",
            "bet-window-prize",
            TournamentSessionPhase.AwaitingChoices,
            1,
            1,
            MatchId,
            CompetitorControllerId,
            Array.Empty<string>(),
            contestants,
            new[] { SpectatorControllerId },
            Array.Empty<TournamentPlayerChoiceData>(),
            new[] { new TournamentRoundData(RoundId, 0, 0, new[] { match }) },
            0,
            0,
            2,
            false,
            false,
            null);
    }
}
