using Coop.Mod.Missions;
using HarmonyLib;
using SandBox.BoardGames;
using SandBox.BoardGames.AI;
using SandBox.BoardGames.MissionLogics;
using SandBox.BoardGames.Pawns;
using SandBox.Conversation.MissionLogics;
using SandBox.Source.Missions.AgentBehaviors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
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
    }

    [HarmonyPatch(typeof(MissionConversationLogic), "StartConversation")]
    public class StartConversationPatch
    {
        static bool Prefix(Agent agent, bool setActionsInstantly, bool isInitialization = false)
        {
            if (agent == null)
            {

            }

            return BoardGameLogic.IsPlayingOtherPlayer == false;
        }
    }

    //[HarmonyPatch(typeof(MissionBoardGameLogic), "SetGameOver")]
    public class SetGameOverPatch
    {
        public static event Action<MissionBoardGameLogic> OnGameOver;
        static bool Prefix(MissionBoardGameLogic __instance, GameOverEnum gameOverInfo)
        {

            OnGameOver?.Invoke(__instance);

            return BoardGameLogic.IsPlayingOtherPlayer == false;
        }
    }

    [HarmonyPatch(typeof(MissionBoardGameLogic), nameof(MissionBoardGameLogic.ForfeitGame))]
    public class ForfeitGamePatch
    {
        public static event Action<MissionBoardGameLogic> OnForfeitGame; 
        static bool Prefix(MissionBoardGameLogic __instance)
        {
            OnForfeitGame?.Invoke(__instance);

            return BoardGameLogic.IsPlayingOtherPlayer == false;
        }
    }

    [HarmonyPatch]
    public class Board
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(BoardGameAIBase), nameof(BoardGameAIBase.CanMakeMove));
            yield return AccessTools.Method(typeof(BoardGameAIBase), nameof(BoardGameAIBase.WantsToForfeit));
            yield return AccessTools.Method(typeof(BoardGameAISeega), nameof(BoardGameAIBase.WantsToForfeit));
        }

        static bool Postfix(bool result)
        {
            if (BoardGameLogic.IsPlayingOtherPlayer) return false;
            return result;
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
