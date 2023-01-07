using Common;
using Common.Messaging;
using Coop.Mod.Patch.BoardGames;
using Missions.Messages.Agents;
using Missions.Messages.BoardGames;
using Missions.Network;
using Missions.Packets.Events;
using SandBox;
using SandBox.BoardGames;
using SandBox.BoardGames.AI;
using SandBox.BoardGames.MissionLogics;
using SandBox.BoardGames.Pawns;
using System;
using System.IO;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using static TaleWorlds.CampaignSystem.CultureObject;

namespace Coop.Mod.Missions
{
    public class BoardGameLogic : IDisposable
    {
        public static bool IsPlayingOtherPlayer { get; set; }
        public static bool IsChallenged { get; private set; }
        public Guid GameId { get; private set; }

        private readonly LiteNetP2PClient _P2PClient;
        private readonly IMessageBroker _messageBroker;
        private readonly MissionBoardGameLogic _boardGameLogic;
        private readonly BoardGameType _boardGameType;

        public BoardGameLogic(
            LiteNetP2PClient P2PClient,
            IMessageBroker messageBroker,
            Guid gameId, 
            MissionBoardGameLogic boardGameLogic, 
            BoardGameType gameType)
        {
            _P2PClient = P2PClient;
            _messageBroker = messageBroker;
            _boardGameLogic = boardGameLogic;
            _boardGameType = gameType;
            GameId = gameId;

            _messageBroker.Subscribe<ForfeitGameMessage>(Handle_ForfeitGameMessage);
            _messageBroker.Subscribe<PawnCapturedMessage>(Handle_PawnCapture);
            _messageBroker.Subscribe<BoardGameMoveRequest>(Handle_MoveRequest);
            _messageBroker.Subscribe<AgentDeleted>(Handle_AgentDeleted);
            _messageBroker.Subscribe<StopConvoAfterGameMessage>(Handle_OnGameOver);

            //StartConversationAfterGamePatch.OnGameOver += OnGameOver;
            ForfeitGamePatch.OnForfeitGame += OnForfeitGame;
            HandlePreMovementStagePatch.OnHandlePreMovementStage += PreMovementStage;
            SetPawnCapturedPatch.OnSetPawnCaptured += OnPawnCapture;
            PreplaceUnitsPatch.OnPreplaceUnits += PreplaceUnits;
            HandlePlayerInputPatch.OnHandlePlayerInput += OnPlayerInput;

        }

        private void Handle_AgentDeleted(MessagePayload<AgentDeleted> payload)
        {
            if(payload.What.Agent.Equals(_boardGameLogic.OpposingAgent))
            {
                _boardGameLogic.AIForfeitGame();
                MBInformationManager.AddQuickInformation(new TextObject("You won! Your opponent has disconnected"));
                Dispose();
            }
        }

        ~BoardGameLogic()
        {
            Dispose();
        }

        public void Dispose()
        {
            //IsPlayingOtherPlayer = false;

            _messageBroker.Unsubscribe<ForfeitGameMessage>(Handle_ForfeitGameMessage);
            _messageBroker.Unsubscribe<PawnCapturedMessage>(Handle_PawnCapture);
            _messageBroker.Unsubscribe<BoardGameMoveRequest>(Handle_MoveRequest);

            //StartConversationAfterGamePatch.OnGameOver -= OnGameOver;
            ForfeitGamePatch.OnForfeitGame -= OnForfeitGame;
            HandlePreMovementStagePatch.OnHandlePreMovementStage -= PreMovementStage;
            SetPawnCapturedPatch.OnSetPawnCaptured -= OnPawnCapture;
            PreplaceUnitsPatch.OnPreplaceUnits -= PreplaceUnits;
            HandlePlayerInputPatch.OnHandlePlayerInput -= OnPlayerInput;
        }

        private static readonly PropertyInfo OpposingAgentPropertyInfo = typeof(MissionBoardGameLogic).GetProperty("OpposingAgent");

        public void StartGame(bool startFirst, Agent opposingAgent)
        {
            IsPlayingOtherPlayer = true;

            // Need for SetGameOver() -> OpposingAgent.GetComponent<CampaignAgentComponent>().AgentNavigator.SpecialTargetTag = this._specialTagOfOpposingHero;
            opposingAgent?.GetComponent<CampaignAgentComponent>().CreateAgentNavigator();

            OpposingAgentPropertyInfo.SetValue(_boardGameLogic, opposingAgent);
            _boardGameLogic.SetBoardGame(_boardGameType);
            _boardGameLogic.SetStartingPlayer(startFirst);
            _boardGameLogic.StartBoardGame();
        }
        
        private void Handle_OnGameOver(MessagePayload<StopConvoAfterGameMessage> payload)
        {
            if (IsPlayingOtherPlayer)
            {
                IsPlayingOtherPlayer = false;
            }
            Dispose();
        }

        private void PreplaceUnits()
        {
            BoardGameSeega seegaBoardGame = (BoardGameSeega)_boardGameLogic.Board;

            var MovePawnToTileDelayedMethod = seegaBoardGame.GetType().GetMethod("MovePawnToTileDelayed", BindingFlags.NonPublic | BindingFlags.Instance);

            MovePawnToTileDelayedMethod.Invoke(seegaBoardGame, new object[] { seegaBoardGame.PlayerTwoUnits[0], seegaBoardGame.GetTile(0, 2), false, false, 0.55f });
            MovePawnToTileDelayedMethod.Invoke(seegaBoardGame, new object[] { seegaBoardGame.PlayerOneUnits[0], seegaBoardGame.GetTile(2, 0), false, false, 0.7f });
            MovePawnToTileDelayedMethod.Invoke(seegaBoardGame, new object[] { seegaBoardGame.PlayerTwoUnits[1], seegaBoardGame.GetTile(4, 2), false, false, 0.85f });
            MovePawnToTileDelayedMethod.Invoke(seegaBoardGame, new object[] { seegaBoardGame.PlayerOneUnits[1], seegaBoardGame.GetTile(2, 4), false, false, 1f });
        }

        private void OnPawnCapture(PawnBase pawn)
        {
            //Only call SetPawnCaptured when it's a forceful remove as a result of no moves available as otherwise it gets handled locally from the move
            //if (!FocusBlockingPawnsPatch.ForceRemove)
            //{
            //    return;
            //}

            int fromIndex = _boardGameLogic.Board.PlayerTwoUnits.IndexOf(pawn);
            PawnCapturedMessage pawnCapturedEvent = new PawnCapturedMessage(GameId, fromIndex);
            _P2PClient.SendAllEvent(pawnCapturedEvent);

            //if (IsPlayingOtherPlayer)
            //{
            //    FocusBlockingPawnsPatch.ForceRemove = false;
            //}
        }

        private void Handle_PawnCapture(MessagePayload<PawnCapturedMessage> payload)
        {
            if (payload.What.GameId == GameId)
            {
                BoardGameBase boardGame = _boardGameLogic.Board;
                PawnBase unitToCapture = boardGame.PlayerOneUnits[payload.What.Index];
                boardGame.SetPawnCaptured(unitToCapture);

                if (!(boardGame is BoardGameSeega))
                {
                    boardGame.GetType().GetMethod("EndTurn", BindingFlags.NonPublic | BindingFlags.Instance)?
                        .Invoke(boardGame, new object[] { });
                }
            }
        }

        private static readonly MethodInfo GetHoveredPawnsMethodInfo = typeof(BoardGameBase).GetMethod("GetHoveredPawnIfAny", BindingFlags.NonPublic | BindingFlags.Instance);
        private void PreMovementStage()
        {
            if (Mission.Current.InputManager.IsHotKeyPressed("BoardGamePawnSelect"))
            {
                PawnBase hoveredPawnIfAny = (PawnBase)GetHoveredPawnsMethodInfo?.Invoke(_boardGameLogic.Board, new object[] { });

                if (hoveredPawnIfAny != null && ((BoardGameKonane)_boardGameLogic.Board).RemovablePawns.Contains(hoveredPawnIfAny))
                {
                    int fromIndex = _boardGameLogic.Board.PlayerOneUnits.IndexOf(hoveredPawnIfAny);
                    PawnCapturedMessage pawnCapturedEvent = new PawnCapturedMessage(GameId, fromIndex);
                    _P2PClient.SendAllEvent(pawnCapturedEvent);
                }
            }
        }

        private void OnPlayerInput(Move move)
        {
            if (!move.IsValid)
            {
                return;
            }

            int FromIndex = _boardGameLogic.Board.PlayerOneUnits.IndexOf(move.Unit);
            int ToIndex = _boardGameLogic.Board.Tiles.IndexOf(move.GoalTile);
            BoardGameMoveRequest boardGameMoveEvent = new BoardGameMoveRequest(GameId, FromIndex, ToIndex);
            _P2PClient.SendAllEvent(boardGameMoveEvent);
        }

        private void Handle_MoveRequest(MessagePayload<BoardGameMoveRequest> payload)
        {
            if (payload.What.GameId == GameId)
            {
                BoardGameBase boardGame = _boardGameLogic.Board;

                var unitToMove = boardGame.PlayerTwoUnits[payload.What.FromIndex];
                var goalTile = boardGame.Tiles[payload.What.ToIndex];

                if (boardGame is BoardGamePuluc)
                {
                    if (payload.What.ToIndex == 11)
                    {
                        goalTile = boardGame.Tiles[11];
                    }
                    else
                    {
                        goalTile = boardGame.Tiles[10 - payload.What.ToIndex];
                    }
                }

                var boardType = boardGame.GetType();

                var movePawnToTileMethod = boardType.GetMethod("MovePawnToTile", BindingFlags.NonPublic | BindingFlags.Instance);
                movePawnToTileMethod.Invoke(boardGame, new object[] { unitToMove, goalTile, false, true });
            }
        }


        //OnGameOver has to be called on Forfeit somewhere
        private void OnForfeitGame(MissionBoardGameLogic missionBoardGame)
        {
            ForfeitGameMessage forfeitMessage = new ForfeitGameMessage(GameId);
            _P2PClient.SendAllEvent(forfeitMessage);
            Dispose();
        }

        private void Handle_ForfeitGameMessage(MessagePayload<ForfeitGameMessage> payload)
        {
            if(payload.What.GameId == GameId)
            {
                _boardGameLogic.AIForfeitGame();
                MBInformationManager.AddQuickInformation(new TextObject("You won! Your opponent has surrendered"));
                Dispose();
            }
        }
    }
}