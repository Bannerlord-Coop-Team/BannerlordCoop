using Common.Network;
using E2E.Tests.Services.MapEvents;
using GameInterface.Services.Entity;
using GameInterface.Services.Locations.BoardGames;
using GameInterface.Services.Locations.BoardGames.Messages;
using GameInterface.Services.Players;
using HarmonyLib;
using SandBox.BoardGames;
using SandBox.BoardGames.MissionLogics;
using System.Linq;
using TaleWorlds.MountAndBlade.Source.Missions.Handlers;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Locations;

public sealed class PlayerBoardGameFlowTests : MapEventTestBase
{
    public PlayerBoardGameFlowTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public void PlayerBoardGame_AcceptedChallenge_StartsAndRelaysOnlyParticipantMoves()
    {
        var harmony = new Harmony("e2e.playerboardgame.clientui");
        harmony.Patch(
            AccessTools.Method(typeof(PlayerBoardGameCoordinator), "Handle_NetworkPlayerBoardGameChallenge"),
            prefix: new HarmonyMethod(typeof(PlayerBoardGameFlowTests), nameof(SuppressClientBoardGameUi)));
        harmony.Patch(
            AccessTools.Method(typeof(PlayerBoardGameCoordinator), "Handle_NetworkPlayerBoardGameStarted"),
            prefix: new HarmonyMethod(typeof(PlayerBoardGameFlowTests), nameof(SuppressClientBoardGameUi)));

        try
        {
            var clients = Clients.Take(2).ToArray();
            var initiatorClient = clients[0];
            var receiverClient = clients[1];

            initiatorClient.Resolve<IControllerIdProvider>().SetControllerId("BoardGameInitiator");
            receiverClient.Resolve<IControllerIdProvider>().SetControllerId("BoardGameReceiver");

            CreatePlayerHeroParty("BoardGameInitiator");
            CreatePlayerHeroParty("BoardGameReceiver");

            Server.Call(() =>
            {
                var playerManager = Server.Resolve<IPlayerManager>();
                playerManager.SetPeer("BoardGameInitiator", initiatorClient.NetPeer);
                playerManager.SetPeer("BoardGameReceiver", receiverClient.NetPeer);
            }, MapEventDisabledMethods);

            Server.NetworkSentMessages.Clear();
            initiatorClient.Call(() => initiatorClient.Resolve<INetwork>().SendAll(
                new NetworkRequestPlayerBoardGame("BoardGameReceiver", boardGameType: 4)));

            var challenge = Server.NetworkSentMessages.GetMessages<NetworkPlayerBoardGameChallenge>().Single();
            Assert.Equal("BoardGameInitiator", challenge.InitiatorControllerId);
            Assert.Equal("BoardGameReceiver", challenge.TargetControllerId);
            Assert.Equal(4, challenge.BoardGameType);

            Server.NetworkSentMessages.Clear();
            receiverClient.Call(() => receiverClient.Resolve<INetwork>().SendAll(
                new NetworkRespondPlayerBoardGameChallenge(challenge.ChallengeId, accepted: true)));

            var started = Server.NetworkSentMessages.GetMessages<NetworkPlayerBoardGameStarted>().Single();
            Assert.Equal("BoardGameInitiator", started.InitiatorControllerId);
            Assert.Equal("BoardGameReceiver", started.ResponderControllerId);
            Assert.Equal(4, started.BoardGameType);

            Server.NetworkSentMessages.Clear();
            initiatorClient.Call(() => initiatorClient.Resolve<INetwork>().SendAll(
                new NetworkPlayerBoardGameMove(started.GameId, "BoardGameInitiator", fromIndex: 2, toIndex: 17)));

            var relayed = Server.NetworkSentMessages.GetMessages<NetworkPlayerBoardGameMove>().Single();
            Assert.Equal(started.GameId, relayed.GameId);
            Assert.Equal("BoardGameInitiator", relayed.SenderControllerId);
            Assert.Equal(2, relayed.FromIndex);
            Assert.Equal(17, relayed.ToIndex);

            Server.NetworkSentMessages.Clear();
            receiverClient.Call(() => receiverClient.Resolve<INetwork>().SendAll(
                new NetworkPlayerBoardGameMove(started.GameId, "BoardGameInitiator", fromIndex: 2, toIndex: 17)));

            Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkPlayerBoardGameMove>());

            receiverClient.Call(() => receiverClient.Resolve<INetwork>().SendAll(
                new NetworkPlayerBoardGameCancelled(started.GameId, "BoardGameReceiver")));

            var cancelled = Server.NetworkSentMessages.GetMessages<NetworkPlayerBoardGameCancelled>().Single();
            Assert.Equal(started.GameId, cancelled.GameId);
            Assert.Equal("BoardGameReceiver", cancelled.SenderControllerId);

            Server.NetworkSentMessages.Clear();
            initiatorClient.Call(() => initiatorClient.Resolve<INetwork>().SendAll(
                new NetworkPlayerBoardGameMove(started.GameId, "BoardGameInitiator", fromIndex: 2, toIndex: 17)));

            Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkPlayerBoardGameMove>());
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
        }
    }

    [Fact]
    public void PlayerBoardGameCompletion_UninstallsHandlerAndSuppressesNativePostGameConversation()
    {
        var handler = new ExplicitBoardGameHandler();

        Server.Call(() =>
        {
            var logic = new MissionBoardGameLogic { Handler = handler };
            var completeLocalGame = AccessTools.Method(typeof(PlayerBoardGameCoordinator), "CompleteLocalGame");
            var startConversationAfterGameEnd = AccessTools.Method(
                typeof(PlayerBoardGameCoordinator),
                "StartConversationAfterGameEndInternal");

            Assert.NotNull(completeLocalGame);
            Assert.NotNull(startConversationAfterGameEnd);
            completeLocalGame.Invoke(
                Server.Resolve<PlayerBoardGameCoordinator>(),
                new object[] { logic, GameOverEnum.PlayerOneWon });

            Assert.False((bool)startConversationAfterGameEnd.Invoke(
                Server.Resolve<PlayerBoardGameCoordinator>(),
                new object[] { logic }));
        });

        Assert.True(handler.Uninstalled);
    }

    public static bool SuppressClientBoardGameUi() => false;

    private sealed class ExplicitBoardGameHandler : IBoardGameHandler
    {
        public bool Uninstalled { get; private set; }

        void IBoardGameHandler.SwitchTurns()
        {
        }

        void IBoardGameHandler.DiceRoll(int roll)
        {
        }

        void IBoardGameHandler.Install()
        {
        }

        void IBoardGameHandler.Uninstall() => Uninstalled = true;

        void IBoardGameHandler.Activate()
        {
        }
    }
}
