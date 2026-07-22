using Common;
using Common.Messaging;
using Common.Network;
using Common.Network.Messages;
using GameInterface.Services.Entity;
using GameInterface.Services.Locations.BoardGames.Messages;
using GameInterface.Services.Locations.Conversations;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using Helpers;
using LiteNetLib;
using SandBox.BoardGames;
using SandBox.BoardGames.MissionLogics;
using SandBox.BoardGames.Pawns;
using SandBox.BoardGames.Tiles;
using SandBox.Conversation.MissionLogics;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.MissionViews;
using TaleWorlds.MountAndBlade.View.Screens;
using TaleWorlds.ScreenSystem;

namespace GameInterface.Services.Locations.BoardGames;

internal sealed class PlayerBoardGameCoordinator : IHandler
{
    private const int ControlRecoveryFrames = 3;

    private sealed class PendingChallenge
    {
        public readonly NetPeer InitiatorPeer;
        public readonly string InitiatorControllerId;
        public readonly string TargetControllerId;
        public readonly int BoardGameType;

        public PendingChallenge(NetPeer initiatorPeer, string initiatorControllerId, string targetControllerId, int boardGameType)
        {
            InitiatorPeer = initiatorPeer;
            InitiatorControllerId = initiatorControllerId;
            TargetControllerId = targetControllerId;
            BoardGameType = boardGameType;
        }
    }

    private sealed class ServerGame
    {
        public readonly string InitiatorControllerId;
        public readonly string ResponderControllerId;

        public ServerGame(string initiatorControllerId, string responderControllerId)
        {
            InitiatorControllerId = initiatorControllerId;
            ResponderControllerId = responderControllerId;
        }

        public bool Contains(string controllerId)
            => controllerId == InitiatorControllerId || controllerId == ResponderControllerId;
    }

    private sealed class ClientGame
    {
        public readonly string GameId;
        public readonly string LocalControllerId;
        public readonly string OtherControllerId;
        public readonly MissionBoardGameLogic Logic;

        public ClientGame(string gameId, string localControllerId, string otherControllerId, MissionBoardGameLogic logic)
        {
            GameId = gameId;
            LocalControllerId = localControllerId;
            OtherControllerId = otherControllerId;
            Logic = logic;
        }
    }

    private static readonly PropertyInfo OpposingAgentProperty = typeof(MissionBoardGameLogic).GetProperty(nameof(MissionBoardGameLogic.OpposingAgent));
    private static readonly PropertyInfo IsGameInProgressProperty = typeof(MissionBoardGameLogic).GetProperty(nameof(MissionBoardGameLogic.IsGameInProgress));
    private static readonly FieldInfo BoardGameStateField = typeof(MissionBoardGameLogic).GetField("_boardGameState", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly object CompletedGameMarker = new object();
    private static readonly MethodInfo MovePawnToTileMethod = typeof(BoardGameBase).GetMethod(
        "MovePawnToTile",
        BindingFlags.Instance | BindingFlags.NonPublic,
        null,
        new[] { typeof(PawnBase), typeof(TileBase), typeof(bool), typeof(bool) },
        null);

    internal static PlayerBoardGameCoordinator Instance { get; private set; }

    private readonly INetwork network;
    private readonly IMessageBroker messageBroker;
    private readonly IPlayerManager playerManager;
    private readonly IObjectManager objectManager;
    private readonly IControllerIdProvider controllerIdProvider;
    private readonly ConcurrentDictionary<string, PendingChallenge> pendingChallenges = new ConcurrentDictionary<string, PendingChallenge>();
    private readonly ConcurrentDictionary<string, ServerGame> serverGames = new ConcurrentDictionary<string, ServerGame>();
    private readonly ConditionalWeakTable<MissionBoardGameLogic, object> completedGames = new ConditionalWeakTable<MissionBoardGameLogic, object>();

    private ClientGame activeGame;
    private bool applyingRemoteResult;
    private Mission controlRecoveryMission;
    private int controlRecoveryFrames;

    public PlayerBoardGameCoordinator(
        IMessageBroker messageBroker,
        INetwork network,
        IPlayerManager playerManager,
        IObjectManager objectManager,
        IControllerIdProvider controllerIdProvider)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.playerManager = playerManager;
        this.objectManager = objectManager;
        this.controllerIdProvider = controllerIdProvider;

        messageBroker.Subscribe<NetworkRequestPlayerBoardGame>(Handle_NetworkRequestPlayerBoardGame);
        messageBroker.Subscribe<NetworkPlayerBoardGameChallenge>(Handle_NetworkPlayerBoardGameChallenge);
        messageBroker.Subscribe<NetworkRespondPlayerBoardGameChallenge>(Handle_NetworkRespondPlayerBoardGameChallenge);
        messageBroker.Subscribe<NetworkPlayerBoardGameStarted>(Handle_NetworkPlayerBoardGameStarted);
        messageBroker.Subscribe<NetworkPlayerBoardGameChallengeDeclined>(Handle_NetworkPlayerBoardGameChallengeDeclined);
        messageBroker.Subscribe<NetworkPlayerBoardGameMove>(Handle_NetworkPlayerBoardGameMove);
        messageBroker.Subscribe<NetworkPlayerBoardGamePawnCaptured>(Handle_NetworkPlayerBoardGamePawnCaptured);
        messageBroker.Subscribe<NetworkPlayerBoardGameFinished>(Handle_NetworkPlayerBoardGameFinished);
        messageBroker.Subscribe<NetworkPlayerBoardGameCancelled>(Handle_NetworkPlayerBoardGameCancelled);
        messageBroker.Subscribe<PlayerDisconnected>(Handle_PlayerDisconnected);

        Instance = this;
    }

    public void Dispose()
    {
        if (Instance == this) Instance = null;

        messageBroker.Unsubscribe<NetworkRequestPlayerBoardGame>(Handle_NetworkRequestPlayerBoardGame);
        messageBroker.Unsubscribe<NetworkPlayerBoardGameChallenge>(Handle_NetworkPlayerBoardGameChallenge);
        messageBroker.Unsubscribe<NetworkRespondPlayerBoardGameChallenge>(Handle_NetworkRespondPlayerBoardGameChallenge);
        messageBroker.Unsubscribe<NetworkPlayerBoardGameStarted>(Handle_NetworkPlayerBoardGameStarted);
        messageBroker.Unsubscribe<NetworkPlayerBoardGameChallengeDeclined>(Handle_NetworkPlayerBoardGameChallengeDeclined);
        messageBroker.Unsubscribe<NetworkPlayerBoardGameMove>(Handle_NetworkPlayerBoardGameMove);
        messageBroker.Unsubscribe<NetworkPlayerBoardGamePawnCaptured>(Handle_NetworkPlayerBoardGamePawnCaptured);
        messageBroker.Unsubscribe<NetworkPlayerBoardGameFinished>(Handle_NetworkPlayerBoardGameFinished);
        messageBroker.Unsubscribe<NetworkPlayerBoardGameCancelled>(Handle_NetworkPlayerBoardGameCancelled);
        messageBroker.Unsubscribe<PlayerDisconnected>(Handle_PlayerDisconnected);

        pendingChallenges.Clear();
        serverGames.Clear();
        activeGame = null;
    }

    internal static bool TryRequestGame(MissionBoardGameLogic logic, Agent opposingAgent)
        => Instance?.TryRequestGameInternal(logic, opposingAgent) == true;

    internal static bool ShouldSuppressAiTurn(BoardGameBase board)
        => Instance?.activeGame?.Logic?.Board == board && board.PlayerTurn == PlayerTurn.PlayerTwo;

    internal static void TrySendMove(BoardGameBase board, Move move)
        => Instance?.TrySendMoveInternal(board, move);

    internal static void TrySendCapturedPawn(BoardGameBase board, PawnBase pawn, bool fake)
        => Instance?.TrySendCapturedPawnInternal(board, pawn, fake);

    internal static bool TryCompleteGame(MissionBoardGameLogic logic, GameOverEnum gameOver)
        => Instance?.TryCompleteGameInternal(logic, gameOver) == true;

    internal static bool IsPlayerGame(MissionBoardGameLogic logic)
        => Instance != null && (Instance.activeGame?.Logic == logic || Instance.completedGames.TryGetValue(logic, out _));

    internal static bool StartConversationAfterGameEnd(MissionBoardGameLogic logic)
        => Instance?.StartConversationAfterGameEndInternal(logic) ?? true;

    internal static void ReassertLocationPlayerControl()
        => Instance?.ReassertLocationPlayerControlInternal();

    private bool TryRequestGameInternal(MissionBoardGameLogic logic, Agent opposingAgent)
    {
        if (ModInformation.IsServer) return false;
        if (!(opposingAgent?.Character is CharacterObject character)) return false;

        var hero = character.HeroObject;
        if (hero == null || !PlayerManager.TryGetControlledObjectInfo(hero, out var controlled))
        {
            completedGames.Remove(logic);
            if (opposingAgent.Controller != AgentControllerType.None) return false;

            ShowMessage("That player is not ready for a board game.");
            return true;
        }

        if (!IsSupportedPlayerBoardGame((int)logic.CurrentBoardGame))
        {
            ShowMessage("Player board games currently support Tablut only.");
            return true;
        }

        if (activeGame != null)
        {
            ShowMessage("You are already playing a board game.");
            return true;
        }

        if (!LocationMissionTracker.IsLocationMission(Mission.Current) ||
            string.IsNullOrEmpty(controlled.ObjectControllerId) ||
            controlled.ObjectControllerId == controllerIdProvider.ControllerId)
        {
            ShowMessage("Player board games are only available with another player in this location.");
            return true;
        }

        network.SendAll(new NetworkRequestPlayerBoardGame(
            controlled.ObjectControllerId,
            (int)logic.CurrentBoardGame));
        ShowMessage("Board game challenge sent.");
        return true;
    }

    private void Handle_NetworkRequestPlayerBoardGame(MessagePayload<NetworkRequestPlayerBoardGame> payload)
    {
        if (ModInformation.IsClient) return;
        if (!(payload.Who is NetPeer initiatorPeer)) return;
        if (!playerManager.TryGetPlayer(initiatorPeer, out var initiator)) return;
        if (!playerManager.TryGetPlayer(payload.What.TargetControllerId, out var target) ||
            target.ControllerId == initiator.ControllerId ||
            !playerManager.IsConnected(target) ||
            !IsSupportedPlayerBoardGame(payload.What.BoardGameType))
        {
            network.Send(initiatorPeer, new NetworkPlayerBoardGameChallengeDeclined(string.Empty, initiator.ControllerId));
            return;
        }

        if (pendingChallenges.Values.Any(challenge =>
                challenge.InitiatorControllerId == initiator.ControllerId ||
                challenge.TargetControllerId == initiator.ControllerId ||
                challenge.InitiatorControllerId == target.ControllerId ||
                challenge.TargetControllerId == target.ControllerId) ||
            serverGames.Values.Any(game => game.Contains(initiator.ControllerId) || game.Contains(target.ControllerId)))
        {
            network.Send(initiatorPeer, new NetworkPlayerBoardGameChallengeDeclined(string.Empty, initiator.ControllerId));
            return;
        }

        var challengeId = Guid.NewGuid().ToString("N");
        var challenge = new PendingChallenge(
            initiatorPeer,
            initiator.ControllerId,
            target.ControllerId,
            payload.What.BoardGameType);

        if (!pendingChallenges.TryAdd(challengeId, challenge)) return;

        network.SendAll(new NetworkPlayerBoardGameChallenge(
            challengeId,
            initiator.ControllerId,
            GetPlayerName(initiator),
            target.ControllerId,
            challenge.BoardGameType));
    }

    private void Handle_NetworkPlayerBoardGameChallenge(MessagePayload<NetworkPlayerBoardGameChallenge> payload)
    {
        if (ModInformation.IsServer) return;

        var challenge = payload.What;
        if (challenge.TargetControllerId != controllerIdProvider.ControllerId) return;

        GameThread.RunSafe(() => ShowChallenge(challenge), context: nameof(Handle_NetworkPlayerBoardGameChallenge));
    }

    private void ShowChallenge(NetworkPlayerBoardGameChallenge challenge)
    {
        if (activeGame != null || !LocationMissionTracker.IsLocationMission(Mission.Current) ||
            !TryFindAgent(challenge.InitiatorControllerId, out _))
        {
            network.SendAll(new NetworkRespondPlayerBoardGameChallenge(challenge.ChallengeId, accepted: false));
            return;
        }

        LocationPlayerInteractionWaitingOverlay.Instance.Hide();
        var gameName = ((CultureObject.BoardGameType)challenge.BoardGameType).ToString();
        InformationManager.ShowInquiry(new InquiryData(
            "Board Game Challenge",
            $"{challenge.InitiatorName} wants to play {gameName}.",
            true,
            true,
            "Accept",
            "Decline",
            () => network.SendAll(new NetworkRespondPlayerBoardGameChallenge(challenge.ChallengeId, accepted: true)),
            () => network.SendAll(new NetworkRespondPlayerBoardGameChallenge(challenge.ChallengeId, accepted: false)),
            string.Empty),
            false,
            false);
    }

    private void Handle_NetworkRespondPlayerBoardGameChallenge(MessagePayload<NetworkRespondPlayerBoardGameChallenge> payload)
    {
        if (ModInformation.IsClient) return;
        if (!(payload.Who is NetPeer responderPeer)) return;
        if (!pendingChallenges.TryRemove(payload.What.ChallengeId, out var challenge)) return;
        if (!playerManager.TryGetPlayer(responderPeer, out var responder) || responder.ControllerId != challenge.TargetControllerId)
        {
            network.Send(challenge.InitiatorPeer, new NetworkPlayerBoardGameChallengeDeclined(payload.What.ChallengeId, challenge.InitiatorControllerId));
            return;
        }

        if (!payload.What.Accepted)
        {
            network.SendAll(new NetworkPlayerBoardGameChallengeDeclined(payload.What.ChallengeId, challenge.InitiatorControllerId));
            return;
        }

        var gameId = Guid.NewGuid().ToString("N");
        if (!serverGames.TryAdd(gameId, new ServerGame(challenge.InitiatorControllerId, challenge.TargetControllerId))) return;

        network.SendAll(new NetworkPlayerBoardGameStarted(
            gameId,
            challenge.InitiatorControllerId,
            challenge.TargetControllerId,
            challenge.BoardGameType));
    }

    private void Handle_NetworkPlayerBoardGameStarted(MessagePayload<NetworkPlayerBoardGameStarted> payload)
    {
        if (ModInformation.IsServer) return;

        var message = payload.What;
        var localControllerId = controllerIdProvider.ControllerId;
        if (message.InitiatorControllerId != localControllerId && message.ResponderControllerId != localControllerId) return;

        GameThread.RunSafe(() => StartLocalGame(message), context: nameof(Handle_NetworkPlayerBoardGameStarted));
    }

    private void StartLocalGame(NetworkPlayerBoardGameStarted message)
    {
        if (activeGame != null) return;
        if (!LocationMissionTracker.IsLocationMission(Mission.Current) ||
            !TryFindAgent(GetOtherControllerId(message), out var opposingAgent))
        {
            network.SendAll(new NetworkPlayerBoardGameCancelled(message.GameId, controllerIdProvider.ControllerId));
            return;
        }

        var logic = Mission.Current.GetMissionBehavior<MissionBoardGameLogic>();
        if (logic == null || logic.IsGameInProgress)
        {
            network.SendAll(new NetworkPlayerBoardGameCancelled(message.GameId, controllerIdProvider.ControllerId));
            return;
        }

        completedGames.Remove(logic);
        activeGame = new ClientGame(message.GameId, controllerIdProvider.ControllerId, GetOtherControllerId(message), logic);
        LocationPlayerInteractionWaitingOverlay.Instance.Hide();
        OpposingAgentProperty.SetValue(logic, opposingAgent);
        logic.SetBoardGame((CultureObject.BoardGameType)message.BoardGameType);
        logic.SetStartingPlayer(message.InitiatorControllerId == controllerIdProvider.ControllerId);
        logic.StartBoardGame();
    }

    private void Handle_NetworkPlayerBoardGameChallengeDeclined(MessagePayload<NetworkPlayerBoardGameChallengeDeclined> payload)
    {
        if (ModInformation.IsServer || payload.What.InitiatorControllerId != controllerIdProvider.ControllerId) return;

        GameThread.RunSafe(() => ShowMessage("Board game challenge declined."), context: nameof(Handle_NetworkPlayerBoardGameChallengeDeclined));
    }

    private void Handle_NetworkPlayerBoardGameMove(MessagePayload<NetworkPlayerBoardGameMove> payload)
    {
        if (ModInformation.IsServer)
        {
            RelayToPlayers(payload.Who, payload.What.GameId, payload.What.SenderControllerId, payload.What);
            return;
        }

        if (!IsRemoteGameMessage(payload.What.GameId, payload.What.SenderControllerId)) return;
        GameThread.RunSafe(() => ApplyRemoteMove(payload.What), context: nameof(Handle_NetworkPlayerBoardGameMove));
    }

    private void Handle_NetworkPlayerBoardGamePawnCaptured(MessagePayload<NetworkPlayerBoardGamePawnCaptured> payload)
    {
        if (ModInformation.IsServer)
        {
            RelayToPlayers(payload.Who, payload.What.GameId, payload.What.SenderControllerId, payload.What);
            return;
        }

        if (!IsRemoteGameMessage(payload.What.GameId, payload.What.SenderControllerId)) return;
        GameThread.RunSafe(() => ApplyRemoteCapture(payload.What), context: nameof(Handle_NetworkPlayerBoardGamePawnCaptured));
    }

    private void Handle_NetworkPlayerBoardGameFinished(MessagePayload<NetworkPlayerBoardGameFinished> payload)
    {
        if (ModInformation.IsServer)
        {
            if (!TryGetServerGame(payload.Who, payload.What.GameId, payload.What.SenderControllerId, out _)) return;
            serverGames.TryRemove(payload.What.GameId, out _);
            network.SendAll(payload.What);
            return;
        }

        if (!IsRemoteGameMessage(payload.What.GameId, payload.What.SenderControllerId)) return;
        GameThread.RunSafe(() => ApplyRemoteResult(payload.What), context: nameof(Handle_NetworkPlayerBoardGameFinished));
    }

    private void Handle_NetworkPlayerBoardGameCancelled(MessagePayload<NetworkPlayerBoardGameCancelled> payload)
    {
        if (ModInformation.IsServer)
        {
            if (!TryGetServerGame(payload.Who, payload.What.GameId, payload.What.SenderControllerId, out _)) return;
            serverGames.TryRemove(payload.What.GameId, out _);
            network.SendAll(payload.What);
            return;
        }

        if (!IsRemoteGameMessage(payload.What.GameId, payload.What.SenderControllerId)) return;
        GameThread.RunSafe(() =>
        {
            if (activeGame == null) return;
            CompleteLocalGame(activeGame.Logic, GameOverEnum.PlayerCanceledTheGame);
            ShowMessage("The other player left the board game.");
        }, context: nameof(Handle_NetworkPlayerBoardGameCancelled));
    }

    private void Handle_PlayerDisconnected(MessagePayload<PlayerDisconnected> payload)
    {
        if (!ModInformation.IsServer) return;

        foreach (var pending in pendingChallenges.Where(pair => ReferenceEquals(pair.Value.InitiatorPeer, payload.What.PlayerId)).ToArray())
        {
            pendingChallenges.TryRemove(pending.Key, out _);
        }

        if (!playerManager.TryGetPlayer(payload.What.PlayerId, out var player)) return;

        foreach (var pending in pendingChallenges.Where(pair => pair.Value.TargetControllerId == player.ControllerId).ToArray())
        {
            if (!pendingChallenges.TryRemove(pending.Key, out var removed)) continue;
            network.Send(removed.InitiatorPeer, new NetworkPlayerBoardGameChallengeDeclined(pending.Key, removed.InitiatorControllerId));
        }

        foreach (var game in serverGames.Where(pair => pair.Value.Contains(player.ControllerId)).ToArray())
        {
            if (!serverGames.TryRemove(game.Key, out _)) continue;
            network.SendAll(new NetworkPlayerBoardGameCancelled(game.Key, player.ControllerId));
        }
    }

    private void TrySendMoveInternal(BoardGameBase board, Move move)
    {
        if (activeGame?.Logic?.Board != board || !move.IsValid) return;

        var fromIndex = board.PlayerOneUnits.IndexOf(move.Unit);
        var toIndex = Array.IndexOf(board.Tiles, move.GoalTile);
        if (fromIndex < 0 || toIndex < 0) return;

        network.SendAll(new NetworkPlayerBoardGameMove(activeGame.GameId, activeGame.LocalControllerId, fromIndex, toIndex));
    }

    private void TrySendCapturedPawnInternal(BoardGameBase board, PawnBase pawn, bool fake)
    {
        if (activeGame?.Logic?.Board != board || fake) return;

        var index = board.PlayerTwoUnits.IndexOf(pawn);
        if (index < 0) return;

        network.SendAll(new NetworkPlayerBoardGamePawnCaptured(activeGame.GameId, activeGame.LocalControllerId, index));
    }

    private bool TryCompleteGameInternal(MissionBoardGameLogic logic, GameOverEnum gameOver)
    {
        if (activeGame?.Logic == logic)
        {
            if (!applyingRemoteResult)
            {
                if (gameOver == GameOverEnum.PlayerCanceledTheGame)
                {
                    network.SendAll(new NetworkPlayerBoardGameCancelled(
                        activeGame.GameId,
                        activeGame.LocalControllerId));
                }
                else
                {
                    network.SendAll(new NetworkPlayerBoardGameFinished(
                        activeGame.GameId,
                        activeGame.LocalControllerId,
                        (int)gameOver));
                }
            }

            CompleteLocalGame(logic, gameOver);
            return true;
        }

        return completedGames.TryGetValue(logic, out _);
    }

    private void ApplyRemoteMove(NetworkPlayerBoardGameMove message)
    {
        var board = activeGame?.Logic?.Board;
        if (board == null || message.FromIndex < 0 || message.FromIndex >= board.PlayerTwoUnits.Count ||
            message.ToIndex < 0 || message.ToIndex >= board.Tiles.Length)
        {
            return;
        }

        var goalTile = board.Tiles[message.ToIndex];
        if (board is BoardGamePuluc && message.ToIndex != 11)
            goalTile = board.Tiles[10 - message.ToIndex];

        MovePawnToTileMethod.Invoke(board, new object[]
        {
            board.PlayerTwoUnits[message.FromIndex],
            goalTile,
            false,
            true
        });
    }

    private void ApplyRemoteCapture(NetworkPlayerBoardGamePawnCaptured message)
    {
        var board = activeGame?.Logic?.Board;
        if (board == null || message.Index < 0 || message.Index >= board.PlayerOneUnits.Count) return;

        board.SetPawnCaptured(board.PlayerOneUnits[message.Index]);
    }

    private void ApplyRemoteResult(NetworkPlayerBoardGameFinished message)
    {
        var logic = activeGame?.Logic;
        if (logic == null) return;

        applyingRemoteResult = true;
        try
        {
            switch ((GameOverEnum)message.GameOver)
            {
                case GameOverEnum.PlayerOneWon:
                    logic.PlayerTwoWon();
                    break;
                case GameOverEnum.PlayerTwoWon:
                    logic.PlayerOneWon();
                    break;
                case GameOverEnum.Draw:
                    logic.GameWasDraw();
                    break;
                default:
                    CompleteLocalGame(logic, GameOverEnum.PlayerCanceledTheGame);
                    break;
            }
        }
        finally
        {
            applyingRemoteResult = false;
        }
    }

    private void CompleteLocalGame(MissionBoardGameLogic logic, GameOverEnum gameOver)
    {
        Mission.Current?.MainAgent?.ClearTargetFrame();
        logic.Board?.SetGameOverInfo(gameOver);
        logic.Handler?.Uninstall();

        BoardGameStateField.SetValue(logic, ToBoardGameState(gameOver));
        IsGameInProgressProperty.SetValue(logic, false);
        OpposingAgentProperty.SetValue(logic, null);
        logic.AIOpponent?.OnSetGameOver();

        completedGames.Remove(logic);
        completedGames.Add(logic, CompletedGameMarker);
        activeGame = null;

        RestoreLocationPlayerControl();
        ArmLocationPlayerControlRecovery();
    }

    private bool StartConversationAfterGameEndInternal(MissionBoardGameLogic logic)
    {
        if (activeGame?.Logic != logic && !completedGames.TryGetValue(logic, out _)) return true;

        RestoreLocationPlayerControl();
        ArmLocationPlayerControlRecovery();
        return false;
    }

    private void ArmLocationPlayerControlRecovery()
    {
        controlRecoveryMission = Mission.Current;
        controlRecoveryFrames = controlRecoveryMission == null ? 0 : ControlRecoveryFrames;
    }

    private void ReassertLocationPlayerControlInternal()
    {
        if (controlRecoveryFrames == 0) return;
        if (controlRecoveryMission != Mission.Current)
        {
            controlRecoveryMission = null;
            controlRecoveryFrames = 0;
            return;
        }

        RestoreLocationPlayerControl();
        if (--controlRecoveryFrames == 0)
            controlRecoveryMission = null;
    }

    private static void RestoreLocationPlayerControl()
    {
        var mission = Mission.Current;
        if (!LocationMissionTracker.IsLocationMission(mission)) return;

        var conversation = mission.GetMissionBehavior<MissionConversationLogic>();
        if (conversation?.ConversationManager?.IsConversationInProgress == true)
            conversation.ConversationManager.EndConversation();

        if (mission.Mode == MissionMode.Conversation && !mission.IsMissionEnding)
            mission.SetMissionMode(MissionMode.Battle, false);

        var localAgent = Agent.Main;
        if (localAgent?.Mission == mission)
        {
            if (localAgent.IsUsingGameObject)
                localAgent.StopUsingGameObject();

            localAgent.SetAsConversationAgent(false);
            localAgent.ClearTargetFrame();
            localAgent.Controller = AgentControllerType.Player;
        }

        if (mission.MainAgentServer != null)
            mission.MainAgentServer.Controller = AgentControllerType.Player;

        var mainAgentController = mission.GetMissionBehavior<MissionMainAgentController>();
        if (mainAgentController != null)
        {
            mainAgentController.IsDisabled = false;
            mainAgentController.Enable();
        }

        if (!(ScreenManager.TopScreen is MissionScreen missionScreen) || missionScreen.SceneLayer == null) return;

        var sceneLayer = missionScreen.SceneLayer;
        sceneLayer.InputRestrictions.ResetInputRestrictions();
        sceneLayer.IsFocusLayer = true;
        ScreenManager.TrySetFocus(sceneLayer);
    }

    private static BoardGameHelper.BoardGameState ToBoardGameState(GameOverEnum gameOver)
    {
        return gameOver switch
        {
            GameOverEnum.PlayerOneWon => BoardGameHelper.BoardGameState.Win,
            GameOverEnum.PlayerTwoWon => BoardGameHelper.BoardGameState.Loss,
            GameOverEnum.Draw => BoardGameHelper.BoardGameState.Draw,
            _ => BoardGameHelper.BoardGameState.None
        };
    }

    private static bool IsSupportedPlayerBoardGame(int boardGameType)
        => boardGameType == (int)CultureObject.BoardGameType.Tablut;

    private bool IsRemoteGameMessage(string gameId, string senderControllerId)
        => activeGame != null &&
           activeGame.GameId == gameId &&
           activeGame.OtherControllerId == senderControllerId;

    private void RelayToPlayers<T>(object sender, string gameId, string senderControllerId, T message)
        where T : ICommand
    {
        if (!TryGetServerGame(sender, gameId, senderControllerId, out _)) return;
        network.SendAll(message);
    }

    private bool TryGetServerGame(object sender, string gameId, string senderControllerId, out ServerGame game)
    {
        game = null;
        if (!(sender is NetPeer peer) || !playerManager.TryGetPlayer(peer, out var player)) return false;
        if (player.ControllerId != senderControllerId) return false;
        return serverGames.TryGetValue(gameId, out game) && game.Contains(senderControllerId);
    }

    private bool TryFindAgent(string controllerId, out Agent agent)
    {
        agent = null;
        if (Mission.Current == null || string.IsNullOrEmpty(controllerId)) return false;

        agent = Mission.Current.Agents.FirstOrDefault(candidate =>
        {
            if (!(candidate.Character is CharacterObject character)) return false;
            var hero = character.HeroObject;
            return hero != null &&
                   PlayerManager.TryGetControlledObjectInfo(hero, out var controlled) &&
                   controlled.ObjectControllerId == controllerId;
        });

        return agent != null;
    }

    private string GetOtherControllerId(NetworkPlayerBoardGameStarted message)
        => message.InitiatorControllerId == controllerIdProvider.ControllerId
            ? message.ResponderControllerId
            : message.InitiatorControllerId;

    private string GetPlayerName(Player player)
    {
        if (objectManager.TryGetObject<Hero>(player.HeroId, out var hero))
            return hero.Name?.ToString() ?? player.ControllerId;

        return player.ControllerId;
    }

    private static void ShowMessage(string message)
        => InformationManager.DisplayMessage(new InformationMessage(message));
}
