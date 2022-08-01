using Common;
using Coop.Mod.Missions.Messages.BoardGames;
using Coop.Mod.Missions.Network;
using Coop.Mod.Patch.BoardGames;
using SandBox.BoardGames;
using SandBox.BoardGames.MissionLogics;
using SandBox.BoardGames.Pawns;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Coop.Mod.Missions
{
    internal class BoardGameLogic : IDisposable
    {
        public static bool IsPlayingOtherPlayer { get; private set; }
        public static bool IsChallenged { get; private set; }
        public Guid GameId { get; private set; }

        private readonly NetworkMessageBroker m_MessageBroker;

        public BoardGameLogic(NetworkMessageBroker messageBroker, Guid gameId)
        {
            m_MessageBroker = messageBroker;
            GameId = gameId;

            m_MessageBroker.Subscribe<ForfeitGameMessage>(Handle_ForfeitGameMessage);
            m_MessageBroker.Subscribe<PawnCapturedMessage>(Handle_PawnCapture);
            m_MessageBroker.Subscribe<BoardGameMoveRequest>(Handle_MoveRequest);

            SetGameOverPatch.OnGameOver += OnGameOver;
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
            IsPlayingOtherPlayer = false;

            m_MessageBroker.Subscribe<ForfeitGameMessage>(Handle_ForfeitGameMessage);
            m_MessageBroker.Subscribe<PawnCapturedMessage>(Handle_PawnCapture);
            m_MessageBroker.Subscribe<BoardGameMoveRequest>(Handle_MoveRequest);

            SetGameOverPatch.OnGameOver -= OnGameOver;
            ForfeitGamePatch.OnForfeitGame -= OnForfeitGame;
            HandlePlayerInputPatch.OnHandlePlayerInput -= OnPlayerInput;
            HandlePreMovementStagePatch.OnHandlePreMovementStage -= PreMovementStage;
            SetPawnCapturedSeegaPatch.OnSetPawnCaptured -= SeegaPawnCapture;
            PreplaceUnitsPatch.OnPreplaceUnits -= PreplaceUnits;
        }

        public void StartGame(bool startFirst)
        {
            MissionBoardGameLogic boardGameLogic = Mission.Current.GetMissionBehavior<MissionBoardGameLogic>();
            boardGameLogic.SetBoardGame(Settlement.CurrentSettlement.Culture.BoardGame);
            boardGameLogic.SetStartingPlayer(startFirst);
            boardGameLogic.StartBoardGame();
        }

        private static readonly FieldInfo GameEndedFieldInfo = typeof(MissionBoardGameLogic).GetField("GameEnded", BindingFlags.NonPublic | BindingFlags.Instance);

        private static readonly PropertyInfo OpposingAgentPropertyInfo = typeof(MissionBoardGameLogic).GetProperty("OpposingAgent");
        private static readonly PropertyInfo IsGameInProgressPropertyInfo = typeof(MissionBoardGameLogic).GetProperty("IsGameInProgress");
        private void OnGameOver(MissionBoardGameLogic boardGameLogic)
        {
            if (IsPlayingOtherPlayer)
            {
                boardGameLogic.Handler?.Uninstall();

                Action eventGameEnded = GameEndedFieldInfo?.GetValue(boardGameLogic) as Action;
                eventGameEnded?.Invoke();

                boardGameLogic.Board.Reset();
                OpposingAgentPropertyInfo.SetValue(boardGameLogic, null);
                IsGameInProgressPropertyInfo.SetValue(boardGameLogic, false);
                Dispose();
            }
        }

        private void PreplaceUnits()
        {
            var boardGameLogic = Mission.Current.GetMissionBehavior<MissionBoardGameLogic>();

            BoardGameSeega seegaBoardGame = (BoardGameSeega)boardGameLogic.Board;

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

            MissionBoardGameLogic boardGameLogic = Mission.Current.GetMissionBehavior<MissionBoardGameLogic>();
            int fromIndex = boardGameLogic.Board.PlayerTwoUnits.IndexOf(pawn);
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

            }
        }

        private static readonly MethodInfo GetHoveredPawnsMethodInfo = typeof(BoardGameBase).GetMethod("GetHoveredPawnIfAny", BindingFlags.NonPublic | BindingFlags.Instance);
        private void PreMovementStage()
        {
            if (Mission.Current.InputManager.IsHotKeyPressed("BoardGamePawnSelect"))
            {
                MissionBoardGameLogic boardGameLogic = Mission.Current.GetMissionBehavior<MissionBoardGameLogic>();

                PawnBase hoveredPawnIfAny = (PawnBase)GetHoveredPawnsMethodInfo?.Invoke(boardGameLogic.Board, new object[] { });

                if (hoveredPawnIfAny != null && ((BoardGameKonane)boardGameLogic.Board).RemovablePawns.Contains(hoveredPawnIfAny))
                {
                    int fromIndex = boardGameLogic.Board.PlayerOneUnits.IndexOf(hoveredPawnIfAny);
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

            MissionBoardGameLogic boardGameLogic = Mission.Current.GetMissionBehavior<MissionBoardGameLogic>();

            int FromIndex = boardGameLogic.Board.PlayerOneUnits.IndexOf(move.Unit);
            int ToIndex = boardGameLogic.Board.Tiles.IndexOf(move.GoalTile);
            BoardGameMoveRequest boardGameMoveEvent = new BoardGameMoveRequest(GameId, FromIndex, ToIndex);
            m_MessageBroker.Publish(boardGameMoveEvent);
        }

        private void Handle_MoveRequest(MessagePayload<BoardGameMoveRequest> payload)
        {
            if (payload.What.GameId == GameId)
            {

            }
        }

        private void OnForfeitGame(MissionBoardGameLogic missionBoardGame)
        {
            ForfeitGameMessage forfeitMessage = new ForfeitGameMessage(GameId);
            m_MessageBroker.Publish(forfeitMessage);
        }

        private void Handle_ForfeitGameMessage(MessagePayload<ForfeitGameMessage> payload)
        {
            if(payload.What.GameId == GameId)
            {
                MissionBoardGameLogic boardGameLogic = Mission.Current.GetMissionBehavior<MissionBoardGameLogic>();
                boardGameLogic.AIForfeitGame();
            }
        }
    }
}
