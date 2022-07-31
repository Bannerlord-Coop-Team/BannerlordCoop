using Common;
using Coop.Mod.Missions.Messages.BoardGames;
using Coop.Mod.Missions.Network;
using Coop.Mod.Patch.Agents;
using Coop.Mod.Patch.BoardGames;
using Coop.NetImpl.LiteNet;
using LiteNetLib;
using NLog;
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
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Coop.Mod.Missions
{
    public class BoardGameManager
    {
        private readonly NLog.Logger m_Logger = LogManager.GetCurrentClassLogger();

        public NetworkMessageBroker MessageBroker { get; private set; }
        public static bool IsPlayingOtherPlayer { get; internal set; }
        public static bool IsChallenged { get; internal set; }

        public BoardGameManager(NetworkMessageBroker messageBroker)
        {
            MessageBroker = messageBroker;

            AgentInteractionPatch.OnAgentInteraction += Handle_OnAgentInteraction;


            MessageBroker.Subscribe<BoardGameChallengeRequest>(Handle_ChallengeRequest);

        }

        private void Handle_OnGameOver(MissionBoardGameLogic boardGameLogic)
        {
            if (IsPlayingOtherPlayer)
            {
                RemoveGameEventHandlers();
                boardGameLogic.Handler?.Uninstall();

                Action eventGameEnded = typeof(MissionBoardGameLogic).GetField("GameEnded", BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.GetValue(boardGameLogic) as Action;
                eventGameEnded?.Invoke();

                boardGameLogic.Board.Reset();
                typeof(MissionBoardGameLogic).GetProperty("OpposingAgent", BindingFlags.Public | BindingFlags.Instance).SetValue(boardGameLogic, null);
                typeof(MissionBoardGameLogic).GetProperty("IsGameInProgress", BindingFlags.Public | BindingFlags.Instance).SetValue(boardGameLogic, false);
            }
        }

        private void RegisterGameEventHandlers()
        {
            SetGameOverPatch.OnGameOver += Handle_OnGameOver;
            ForfeitGamePatch.OnForfeitGame += Handle_OnForfeitGame;
            HandlePlayerInputPatch.OnHandlePlayerInput += Handle_OnPlayerInput;
            HandlePreMovementStagePatch.OnHandlePreMovementStage += Handle_PreMovementStage;
            SetPawnCapturedSeegaPatch.OnSetPawnCaptured += Handle_SeegaPawnCapture;
            PreplaceUnitsPatch.OnPreplaceUnits += Handle_PreplaceUnits;
        }

        private void Handle_PreplaceUnits()
        {

            var boardGameLogic = Mission.Current.GetMissionBehavior<MissionBoardGameLogic>();

            BoardGameSeega seegaBoardGame = (BoardGameSeega)boardGameLogic.Board;

            var MovePawnToTileDelayedMethod = seegaBoardGame.GetType().GetMethod("MovePawnToTileDelayed", BindingFlags.NonPublic | BindingFlags.Instance);

            MovePawnToTileDelayedMethod.Invoke(seegaBoardGame, new object[] { seegaBoardGame.PlayerTwoUnits[0], seegaBoardGame.GetTile(0, 2), false, false, 0.55f });
            MovePawnToTileDelayedMethod.Invoke(seegaBoardGame, new object[] { seegaBoardGame.PlayerOneUnits[0], seegaBoardGame.GetTile(2, 0), false, false, 0.7f });
            MovePawnToTileDelayedMethod.Invoke(seegaBoardGame, new object[] { seegaBoardGame.PlayerTwoUnits[1], seegaBoardGame.GetTile(4, 2), false, false, 0.85f });
            MovePawnToTileDelayedMethod.Invoke(seegaBoardGame, new object[] { seegaBoardGame.PlayerOneUnits[1], seegaBoardGame.GetTile(2, 4), false, false, 1f });

        }

        private void Handle_SeegaPawnCapture(PawnBase obj)
        {
            //Only call SetPawnCaptured when it's a forceful remove as a result of no moves available as otherwise it gets handled locally from the move
            if (!FocusBlockingPawnsPatch.ForceRemove)
            {
                return;
            }

            MissionBoardGameLogic boardGameLogic = Mission.Current.GetMissionBehavior<MissionBoardGameLogic>();
            PawnCapturedEvent pawnCapturedEvent = new PawnCapturedEvent();

            //Probably the reason it does not work, too tired at the moment to debug this
            pawnCapturedEvent.fromIndex = boardGameLogic.Board.PlayerTwoUnits.IndexOf(pawn);
            InformationManager.DisplayMessage(new InformationMessage("PlayerOneUnitsIndex: " + pawnCapturedEvent.fromIndex));

            var netDataWriter = new NetDataWriter();
            netDataWriter.Put((uint)MessageType.PawnCapture);

            using (var memoryStream = new MemoryStream())
            {
                Serializer.SerializeWithLengthPrefix<PawnCapturedEvent>(memoryStream, pawnCapturedEvent, PrefixStyle.Fixed32BigEndian);
                netDataWriter.Put(memoryStream.ToArray());
            }


            MissionNetworkBehavior.client.SendToAll(netDataWriter, DeliveryMethod.ReliableSequenced);
            if (BoardGameManager.IsPlayingOtherPlayer)
            {
                FocusBlockingPawnsPatch.ForceRemove = false;
            }


        }

        private void Handle_PreMovementStage()
        {
            if(Mission.Current.InputManager.IsHotKeyPressed("BoardGamePawnSelect"))
            {
                MissionBoardGameLogic boardGameLogic = Mission.Current.GetMissionBehavior<MissionBoardGameLogic>();
                PawnCapturedEvent pawnCapturedEvent = new PawnCapturedEvent();

                PawnBase hoveredPawnIfAny = (PawnBase)typeof(BoardGameBase).GetMethod("GetHoveredPawnIfAny", BindingFlags.NonPublic | BindingFlags.Instance)?
                    .Invoke(boardGameLogic.Board, new object[] { });

                if (hoveredPawnIfAny != null && ((BoardGameKonane)boardGameLogic.Board).RemovablePawns.Contains(hoveredPawnIfAny))
                {
                    pawnCapturedEvent.fromIndex = boardGameLogic.Board.PlayerOneUnits.IndexOf(hoveredPawnIfAny);

                    var netDataWriter = new NetDataWriter();
                    netDataWriter.Put((uint)MessageType.PawnCapture);

                    using (var memoryStream = new MemoryStream())
                    {
                        Serializer.SerializeWithLengthPrefix<PawnCapturedEvent>(memoryStream, pawnCapturedEvent, PrefixStyle.Fixed32BigEndian);
                        netDataWriter.Put(memoryStream.ToArray());
                    }
                    //InformationManager.DisplayMessage(new InformationMessage(
                    //   $"Sending PawnCapture to server relay, unit id:{pawnCapturedEvent.fromIndex}"));

                    MissionNetworkBehavior.client.SendToAll(netDataWriter, DeliveryMethod.ReliableSequenced);

                }
            }
        }

        private void Handle_OnPlayerInput(Move obj)
        {
            if (!__result.IsValid)
            {
                return;
            }

            MissionBoardGameLogic boardGameLogic = Mission.Current.GetMissionBehavior<MissionBoardGameLogic>();

            BoardGameMoveRequest boardGameMoveEvent = new BoardGameMoveRequest()
            {
                FromIndex = boardGameLogic.Board.PlayerOneUnits.IndexOf(__result.Unit),
                ToIndex = boardGameLogic.Board.Tiles.IndexOf(__result.GoalTile)
            };

            broker.Publish(__instance, boardGameMoveEvent);
        }

        private void Handle_OnForfeitGame(MissionBoardGameLogic obj)
        {
            var otherAgent = __instance.OpposingAgent;
            var otherAgentId = ClientAgentManager.Instance().GetIdFromIndex(otherAgent.Index);

            if (otherAgentId == null)
                return;

            NetDataWriter writer = new NetDataWriter();
            writer.Put((uint)MessageType.BoardGameForfeit);
            writer.Put(otherAgentId);

            client.SendToAll(writer, DeliveryMethod.ReliableUnordered);
        }

        private void RemoveGameEventHandlers()
        {
            SetGameOverPatch.OnGameOver -= Handle_OnGameOver;
            ForfeitGamePatch.OnForfeitGame -= Handle_OnForfeitGame;
            HandlePlayerInputPatch.OnHandlePlayerInput -= Handle_OnPlayerInput;
            HandlePreMovementStagePatch.OnHandlePreMovementStage -= Handle_PreMovementStage;
            SetPawnCapturedSeegaPatch.OnSetPawnCaptured -= Handle_SeegaPawnCapture;
            PreplaceUnitsPatch.OnPreplaceUnits -= Handle_PreplaceUnits;
        }

        private void Handle_OnAgentInteraction(Agent sender, Agent other)
        {

            InformationManager.ShowInquiry(new InquiryData("Board Game Challenge", string.Empty, true, true, "Challenge", "Pussy out",
                new Action(() => { SendGameRequest(sender, other); }), new Action(() => { })));
        }



        private void SendGameRequest(Agent sender, Agent other)
        {
            if (MissionClient.AgentToId.TryGetValue(sender, out Guid senderGuid) && MissionClient.AgentToId.TryGetValue(other, out Guid otherGuid))
            {
                BoardGameChallengeRequest request = new BoardGameChallengeRequest(senderGuid, otherGuid);
                MessageBroker.Subscribe<BoardGameChallengeResponse>(Handle_ChallengeResponse);
                MessageBroker.Publish(request);
            }
            else
            {
                m_Logger.Warn("SendGameRequest failed to send");
            }
        }

        private void Handle_ChallengeRequest(MessagePayload<BoardGameChallengeRequest> payload)
        {
            Guid sender = payload.What.TargetPlayer;
            Guid other = payload.What.RequestingPlayer;
            NetPeer netPeer = payload.Who as NetPeer ?? throw new InvalidCastException("Payload 'Who' was not of type NetPeer");

            InformationManager.ShowInquiry(new InquiryData("Board Game Challenge", string.Empty, true, true, "Accept", "Pussy out",
                new Action(() => { AcceptGameRequest(sender, other, netPeer); }), new Action(() => { DenyGameRequest(sender, other, netPeer); })));
        }

        private void AcceptGameRequest(Guid sender, Guid other, NetPeer netPeer)
        {
            BoardGameChallengeResponse response = new BoardGameChallengeResponse(sender, other, true);
            MessageBroker.Publish<BoardGameChallengeResponse>(response, netPeer);

            //Has to do same thing as if (accepted) in Handle_ChallengeResponse
            StartGame(false);
        }

        private void DenyGameRequest(Guid sender, Guid other, NetPeer netPeer)
        {
            BoardGameChallengeResponse response = new BoardGameChallengeResponse(sender, other, false);
            MessageBroker.Publish<BoardGameChallengeResponse>(response, netPeer);
        }

        private void Handle_ChallengeResponse(MessagePayload<BoardGameChallengeResponse> payload)
        {
            bool accepted = payload.What.Accepted;
            if (accepted)
            {
                StartGame(true);
            }
        }

        private void StartGame(bool StartingPlayer)
        {
            IsPlayingOtherPlayer = true;
            RegisterGameEventHandlers();
            MissionBoardGameLogic boardGameLogic = Mission.Current.GetMissionBehavior<MissionBoardGameLogic>();
            boardGameLogic.SetBoardGame(Settlement.CurrentSettlement.Culture.BoardGame);
            boardGameLogic.SetStartingPlayer(StartingPlayer);
            boardGameLogic.StartBoardGame();
            
        }
    }
}
