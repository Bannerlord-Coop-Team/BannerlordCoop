using Common.Messaging;
using HarmonyLib;
using LiteNetLib;
using LiteNetLib.Utils;
using MissionsShared;
using ProtoBuf;
using SandBox.BoardGames;
using SandBox.BoardGames.MissionLogics;
using SandBox.BoardGames.Pawns;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace CoopTestMod
{
    public class BoardGamePlayerInputPatches
    {
        private static bool forceRemove = false;

        private static IMessageBroker broker; /* TODO Point to MissionManager broker */
    
        [HarmonyPatch(typeof(BoardGameBase), "HandlePlayerInput")]
        public class HandlePlayerInputPatch
        {
            static void Postfix(ref BoardGameBase __instance, ref Move __result)
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

                broker.Publish(boardGameMoveEvent);
            }
        }

        [HarmonyPatch(typeof(BoardGameKonane), "HandlePreMovementStage")]
        public class HandlePreMovementStagePatch
        {
            public static void Prefix(ref BoardGameKonane __instance)
            {

                if (Mission.Current.InputManager.IsHotKeyPressed("BoardGamePawnSelect"))
                {
                    MissionBoardGameLogic boardGameLogic = Mission.Current.GetMissionBehavior<MissionBoardGameLogic>();

                    PawnBase hoveredPawnIfAny = (PawnBase)typeof(BoardGameBase).GetMethod("GetHoveredPawnIfAny", BindingFlags.NonPublic | BindingFlags.Instance)?
                        .Invoke(boardGameLogic.Board, new object[] { });

                    if (hoveredPawnIfAny != null && ((BoardGameKonane)boardGameLogic.Board).RemovablePawns.Contains(hoveredPawnIfAny))
                    {
                        PawnCapturedRequest pawnCapturedEvent = new PawnCapturedRequest()
                        {
                            FromIndex = boardGameLogic.Board.PlayerOneUnits.IndexOf(hoveredPawnIfAny)
                        };

                        broker.Publish(pawnCapturedEvent);
                    }
                }
            }
        }

        [HarmonyPatch(typeof(BoardGameSeega), "FocusBlockingPawns")]
        public class FocusBlockingPawnsPatch
        {
            public static void Postfix()
            {
                forceRemove = true;
            }
        }

        [HarmonyPatch(typeof(BoardGameSeega), "SetPawnCaptured")]
        public class SetPawnCapturedSeegaPatch
        {

            public static void Prefix(PawnBase pawn, bool aiSimulation)
            {
                //Only calls SetPawnCaptured when it's a forceful remove as a result of no moves available as otherwise it gets handled locally from the move
                if (!forceRemove)
                {
                    return;
                }

                MissionBoardGameLogic boardGameLogic = Mission.Current.GetMissionBehavior<MissionBoardGameLogic>();
                PawnCapturedRequest pawnCapturedEvent = new PawnCapturedRequest()
                {
                    FromIndex = boardGameLogic.Board.PlayerTwoUnits.IndexOf(pawn)
                };

                broker.Publish(pawnCapturedEvent);

                forceRemove = false;
            }
        }

        [HarmonyPatch(typeof(BoardGameSeega), "PreplaceUnits")]
        public class PreplaceUnitsPatch
        {

            public static bool isChallenged = false;

            static bool Prefix()
            {
                if (isChallenged) { return true; }

                var boardGameLogic = Mission.Current.GetMissionBehavior<MissionBoardGameLogic>();

                BoardGameSeega seegaBoardGame = (BoardGameSeega)boardGameLogic.Board;

                var MovePawnToTileDelayedMethod = seegaBoardGame.GetType().GetMethod("MovePawnToTileDelayed", BindingFlags.NonPublic | BindingFlags.Instance);

                MovePawnToTileDelayedMethod.Invoke(seegaBoardGame, new object[] { seegaBoardGame.PlayerTwoUnits[0], seegaBoardGame.GetTile(0, 2), false, false, 0.55f });
                MovePawnToTileDelayedMethod.Invoke(seegaBoardGame, new object[] { seegaBoardGame.PlayerOneUnits[0], seegaBoardGame.GetTile(2, 0), false, false, 0.7f });
                MovePawnToTileDelayedMethod.Invoke(seegaBoardGame, new object[] { seegaBoardGame.PlayerTwoUnits[1], seegaBoardGame.GetTile(4, 2), false, false, 0.85f });
                MovePawnToTileDelayedMethod.Invoke(seegaBoardGame, new object[] { seegaBoardGame.PlayerOneUnits[1], seegaBoardGame.GetTile(2, 4), false, false, 1f });

                return false;
            }
        }
    }
}
