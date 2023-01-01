using Coop.Mod.Missions;
using HarmonyLib;
using Missions.Network;
using SandBox.BoardGames;
using SandBox.BoardGames.AI;
using SandBox.BoardGames.MissionLogics;
using SandBox.BoardGames.Pawns;
using SandBox.BoardGames.Tiles;
using SandBox.Source.Missions.AgentBehaviors;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Coop.Mod.Patch.BoardGames
{

    [HarmonyPatch(typeof(BoardGameAgentBehavior), "RemoveBoardGameBehaviorOfAgent")]
    public class RemoveBoardGameBehaviorOfAgentPatch
    {
        static bool Prefix(Agent ownerAgent)
        {

            //Somewhat ugly way to not break forfeit/win, might be issues with opposingAgent have not checked
            return BoardGameLogic.IsPlayingOtherPlayer == false;
        }

        static void Postfix(Agent ownerAgent)
        {
            BoardGameLogic.IsPlayingOtherPlayer = false;
        }
    }

    [HarmonyPatch(typeof(MissionBoardGameLogic), "StartConversationWithOpponentAfterGameEnd")]
    public class StartConversationAfterGamePatch
    {
        public static event Action<MissionBoardGameLogic> OnGameOver;
        static bool Prefix(MissionBoardGameLogic __instance, Agent conversationAgent)
        {
            if (NetworkAgentRegistry.AgentToId.ContainsKey(conversationAgent))
            {
                OnGameOver?.Invoke(__instance);

                return false;
            }

            else
            {
                return true;
            }
        }
    }

    [HarmonyPatch(typeof(BoardGameBase), "HandlePlayerInput")]
    public class HandlePlayerInputPatch
    {
        public static event Action<Move> OnHandlePlayerInput;
        static void Postfix(ref BoardGameBase __instance, ref Move __result)
        {
            OnHandlePlayerInput?.Invoke(__result);
        }
    }

    [HarmonyPatch(typeof(MissionBoardGameLogic), nameof(MissionBoardGameLogic.ForfeitGame))]
    public class ForfeitGamePatch
    {
        public static event Action<MissionBoardGameLogic> OnForfeitGame; 
        static bool Prefix(MissionBoardGameLogic __instance)
        {
            if (BoardGameLogic.IsPlayingOtherPlayer)
            {
                OnForfeitGame?.Invoke(__instance);
            }

             return true;

        }
    }

    [HarmonyPatch]
    public class Board
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(BoardGameAIBase), nameof(BoardGameAIBase.WantsToForfeit));
            yield return AccessTools.Method(typeof(BoardGameAISeega), nameof(BoardGameAIBase.WantsToForfeit));
        }

        static bool Postfix(bool result)
        {
            if (BoardGameLogic.IsPlayingOtherPlayer) return false;
            return result;
        }
    }

    [HarmonyPatch(typeof(BoardGameAIBase), "CalculateMovementStageMoveOnSeparateThread")]
    public class CalculateMovePatch
    {
        public static bool Prefix()
        {
            if (!BoardGameLogic.IsPlayingOtherPlayer) return true;

            return false;
        }
    }

    [HarmonyPatch(typeof(BoardGameKonane), "HandlePreMovementStage")]
    public class HandlePreMovementStagePatch
    {
        public static event Action OnHandlePreMovementStage;
        public static void Prefix()
        {
            OnHandlePreMovementStage?.Invoke();
        }
    }

    [HarmonyPatch(typeof(BoardGameSeega), "FocusBlockingPawns")]
    public class FocusBlockingPawnsPatch
    {
        public static bool ForceRemove = false;
        public static void Postfix()
        {
            if (BoardGameLogic.IsPlayingOtherPlayer)
            {
                ForceRemove = true;
            }
        }
    }

    [HarmonyPatch(typeof(BoardGameSeega), "SetPawnCaptured")]
    public class SetPawnCapturedSeegaPatch
    {
        public static event Action<PawnBase> OnSetPawnCaptured;
        public static void Prefix(PawnBase pawn, bool aiSimulation)
        {
            OnSetPawnCaptured?.Invoke(pawn);
        }
    }

    [HarmonyPatch(typeof(BoardGameSeega), "PreplaceUnits")]
    public class PreplaceUnitsPatch
    {
        public static event Action OnPreplaceUnits;

        static bool Prefix()
        {

            if (BoardGameLogic.IsPlayingOtherPlayer && BoardGameLogic.IsChallenged) { return false; }

            OnPreplaceUnits?.Invoke();

            return true;

        }
    }
}
