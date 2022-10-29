using Common;
using Coop.Mod.Patch.BoardGames;
using Missions.Messages.BoardGames;
using Missions.Network;
using NLog;
using SandBox;
using SandBox.BoardGames;
using SandBox.BoardGames.MissionLogics;
using SandBox.BoardGames.Pawns;
using SandBox.GauntletUI.Missions;
using System;
using System.Reflection;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using static TaleWorlds.CampaignSystem.CultureObject;

namespace Coop.Mod.Missions
{
    public class BoardGameLogic : IDisposable
    {
        private static NLog.Logger m_Logger = LogManager.GetCurrentClassLogger();
        public static bool IsPlayingOtherPlayer { get; set; }
        public static bool IsChallenged { get; private set; }
        public Guid GameId { get; private set; }

        private readonly INetworkMessageBroker m_MessageBroker;
        private readonly MissionBoardGameLogic m_BoardGameLogic;
        private readonly BoardGameType m_BoardGameType;

        public BoardGameLogic(INetworkMessageBroker messageBroker, Guid gameId, MissionBoardGameLogic boardGameLogic, BoardGameType gameType)
        {
            m_MessageBroker = messageBroker;
            m_BoardGameLogic = boardGameLogic;
            m_BoardGameType = gameType;
            GameId = gameId;

            m_MessageBroker.Subscribe<ForfeitGameMessage>(Handle_ForfeitGameMessage);
            m_MessageBroker.Subscribe<PawnCapturedMessage>(Handle_PawnCapture);
            m_MessageBroker.Subscribe<BoardGameMoveRequest>(Handle_MoveRequest);

            StartConversationAfterGamePatch.OnGameOver += OnGameOver;
            ForfeitGamePatch.OnForfeitGame += OnForfeitGame;
            HandlePlayerInputPatch.OnHandlePlayerInput += OnPlayerInput;
            HandlePreMovementStagePatch.OnHandlePreMovementStage += PreMovementStage;
            SetPawnCapturedSeegaPatch.OnSetPawnCaptured += SeegaPawnCapture;
            PreplaceUnitsPatch.OnPreplaceUnits += PreplaceUnits;
            
        }

        ~BoardGameLogic()
        {
            Dispose();
        }

        public void Dispose()
        {
            //IsPlayingOtherPlayer = false;

            m_MessageBroker.Unsubscribe<ForfeitGameMessage>(Handle_ForfeitGameMessage);
            m_MessageBroker.Unsubscribe<PawnCapturedMessage>(Handle_PawnCapture);
            m_MessageBroker.Unsubscribe<BoardGameMoveRequest>(Handle_MoveRequest);

            StartConversationAfterGamePatch.OnGameOver -= OnGameOver;
            ForfeitGamePatch.OnForfeitGame -= OnForfeitGame;
            HandlePlayerInputPatch.OnHandlePlayerInput -= OnPlayerInput;
            HandlePreMovementStagePatch.OnHandlePreMovementStage -= PreMovementStage;
            SetPawnCapturedSeegaPatch.OnSetPawnCaptured -= SeegaPawnCapture;
            PreplaceUnitsPatch.OnPreplaceUnits -= PreplaceUnits;
        }

        private static readonly PropertyInfo OpposingAgentPropertyInfo = typeof(MissionBoardGameLogic).GetProperty("OpposingAgent");

        public void StartGame(bool startFirst, Agent opposingAgent)
        {
            IsPlayingOtherPlayer = true;

            // Need for SetGameOver() -> OpposingAgent.GetComponent<CampaignAgentComponent>().AgentNavigator.SpecialTargetTag = this._specialTagOfOpposingHero;
            opposingAgent?.GetComponent<CampaignAgentComponent>().CreateAgentNavigator();

            OpposingAgentPropertyInfo.SetValue(m_BoardGameLogic, opposingAgent);
            m_BoardGameLogic.SetBoardGame(m_BoardGameType);
            m_BoardGameLogic.SetStartingPlayer(startFirst);
            m_BoardGameLogic.StartBoardGame();
        }
        
        private void OnGameOver(MissionBoardGameLogic boardGameLogic)
        {
            if (IsPlayingOtherPlayer)
            {
                IsPlayingOtherPlayer = false;
            }
            Dispose();
        }

        private void PreplaceUnits()
        {
            BoardGameSeega seegaBoardGame = (BoardGameSeega)m_BoardGameLogic.Board;

            var MovePawnToTileDelayedMethod = seegaBoardGame.GetType().GetMethod("MovePawnToTileDelayed", BindingFlags.NonPublic | BindingFlags.Instance);

            MovePawnToTileDelayedMethod.Invoke(seegaBoardGame, new object[] { seegaBoardGame.PlayerTwoUnits[0], seegaBoardGame.GetTile(0, 2), false, false, 0.55f });
            MovePawnToTileDelayedMethod.Invoke(seegaBoardGame, new object[] { seegaBoardGame.PlayerOneUnits[0], seegaBoardGame.GetTile(2, 0), false, false, 0.7f });
            MovePawnToTileDelayedMethod.Invoke(seegaBoardGame, new object[] { seegaBoardGame.PlayerTwoUnits[1], seegaBoardGame.GetTile(4, 2), false, false, 0.85f });
            MovePawnToTileDelayedMethod.Invoke(seegaBoardGame, new object[] { seegaBoardGame.PlayerOneUnits[1], seegaBoardGame.GetTile(2, 4), false, false, 1f });
        }

        private void SeegaPawnCapture(PawnBase pawn)
        {
            //Only call SetPawnCaptured when it's a forceful remove as a result of no moves available as otherwise it gets handled locally from the move
            if (!FocusBlockingPawnsPatch.ForceRemove)
            {
                return;
            }

            int fromIndex = m_BoardGameLogic.Board.PlayerTwoUnits.IndexOf(pawn);
            PawnCapturedMessage pawnCapturedEvent = new PawnCapturedMessage(GameId, fromIndex);
            m_MessageBroker.Publish(pawnCapturedEvent);

            if (IsPlayingOtherPlayer)
            {
                FocusBlockingPawnsPatch.ForceRemove = false;
            }
        }

        private void Handle_PawnCapture(MessagePayload<PawnCapturedMessage> payload)
        {
            if (payload.What.GameId == GameId)
            {
                BoardGameBase boardGame = m_BoardGameLogic.Board;
                PawnBase unitToCapture;

                if (boardGame is BoardGameSeega)
                {
                    unitToCapture = boardGame.PlayerOneUnits[payload.What.Index];
                }
                else
                {
                    unitToCapture = boardGame.PlayerTwoUnits[payload.What.Index];
                }

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
                PawnBase hoveredPawnIfAny = (PawnBase)GetHoveredPawnsMethodInfo?.Invoke(m_BoardGameLogic.Board, new object[] { });

                if (hoveredPawnIfAny != null && ((BoardGameKonane)m_BoardGameLogic.Board).RemovablePawns.Contains(hoveredPawnIfAny))
                {
                    int fromIndex = m_BoardGameLogic.Board.PlayerOneUnits.IndexOf(hoveredPawnIfAny);
                    PawnCapturedMessage pawnCapturedEvent = new PawnCapturedMessage(GameId, fromIndex);
                    m_MessageBroker.Publish(pawnCapturedEvent);
                }
            }
        }

        private void OnPlayerInput(Move move)
        {
            if (!move.IsValid)
            {
                return;
            }

            int FromIndex = m_BoardGameLogic.Board.PlayerOneUnits.IndexOf(move.Unit);
            int ToIndex = m_BoardGameLogic.Board.Tiles.IndexOf(move.GoalTile);
            BoardGameMoveRequest boardGameMoveEvent = new BoardGameMoveRequest(GameId, FromIndex, ToIndex);
            m_MessageBroker.Publish(boardGameMoveEvent);
        }

        private void Handle_MoveRequest(MessagePayload<BoardGameMoveRequest> payload)
        {
            if (payload.What.GameId == GameId)
            {
                BoardGameBase boardGame = m_BoardGameLogic.Board;

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

                boardType.GetProperty("SelectedUnit", BindingFlags.NonPublic | BindingFlags.Instance)?
                    .SetValue(boardGame, unitToMove);

                var movePawnToTileMethod = boardType.GetMethod("MovePawnToTile", BindingFlags.NonPublic | BindingFlags.Instance);
                movePawnToTileMethod?.Invoke(boardGame, new object[] { unitToMove, goalTile, false, true });
            }
        }


        //OnGameOver has to be called on Forfeit somewhere
        private void OnForfeitGame(MissionBoardGameLogic missionBoardGame)
        {
            ForfeitGameMessage forfeitMessage = new ForfeitGameMessage(GameId);
            m_MessageBroker.Publish(forfeitMessage);
            Dispose();
            //missionBoardGame.Board.SetGameOverInfo(GameOverEnum.PlayerTwoWon);
            //missionBoardGame.SetGameOver(missionBoardGame.Board.GameOverInfo);
        }

        private void Handle_ForfeitGameMessage(MessagePayload<ForfeitGameMessage> payload)
        {
            if(payload.What.GameId == GameId)
            {
                m_BoardGameLogic.SetGameOver(GameOverEnum.PlayerOneWon);
                Dispose();
            }
        }
    }
}
//m_BoardGameLogic.AIForfeitGame();
//Dispose();