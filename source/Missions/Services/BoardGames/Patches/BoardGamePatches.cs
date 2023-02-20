using Common.Messaging;
using HarmonyLib;
using SandBox;
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
using Missions.Services.BoardGames;
using Missions.Services.Network;
using Missions.Services.BoardGames.Messages;

namespace Missions.Services.BoardGames.Patches
{

    [HarmonyPatch(typeof(BoardGameAgentBehavior), nameof(BoardGameAgentBehavior.RemoveBoardGameBehaviorOfAgent))]
    public class RemoveBoardGameBehaviorOfAgentPatch
    {
        static bool Prefix(Agent ownerAgent)
        {
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
        private static readonly PropertyInfo AgentNavigatorPropertyInfo = typeof(CampaignAgentComponent).GetProperty("AgentNavigator");
        static bool Prefix(Agent conversationAgent)
        {
            if (NetworkAgentRegistry.Instance.AgentToId.ContainsKey(conversationAgent))
            {
                StopConvoAfterGameMessage message = new StopConvoAfterGameMessage();

                MessageBroker.Instance.Publish(conversationAgent, message);

                //Set AgentNavigator to null as this gets set in SetGameOver by default and breaks all future interactions
                AgentNavigatorPropertyInfo.SetValue(conversationAgent.GetComponent<CampaignAgentComponent>(), null);
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
        static void Postfix(ref BoardGameBase __instance, ref Move __result)
        {
            BoardGameMoveMessage message = new BoardGameMoveMessage(__result);
            MessageBroker.Instance.Publish(Agent.Main, message);
        }
    }

    [HarmonyPatch(typeof(MissionBoardGameLogic), nameof(MissionBoardGameLogic.ForfeitGame))]
    public class ForfeitGamePatch
    {
        static bool Prefix(MissionBoardGameLogic __instance)
        {
            if (BoardGameLogic.IsPlayingOtherPlayer)
            {
                OnForfeitMessage message = new OnForfeitMessage();
                MessageBroker.Instance.Publish(Agent.Main, message);
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
            return !BoardGameLogic.IsPlayingOtherPlayer;
        }
    }

    [HarmonyPatch(typeof(BoardGameKonane), "HandlePreMovementStage")]
    public class HandlePreMovementStagePatch
    {
        public static void Prefix()
        {
            OnHandlePreMovementStageMessage message = new OnHandlePreMovementStageMessage();
            MessageBroker.Instance.Publish(Agent.Main, message);
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

    [HarmonyPatch(typeof(BoardGameBase), nameof(BoardGameBase.SetPawnCaptured))]
    public class SetPawnCapturedPatch
    {
        public static void Postfix(PawnBase pawn, bool fake)
        {
            OnSetPawnCapturedMessage message = new OnSetPawnCapturedMessage(pawn);
            MessageBroker.Instance.Publish(Agent.Main, message);
        }
    }

    [HarmonyPatch(typeof(BoardGameSeega), "PreplaceUnits")]
    public class PreplaceUnitsPatch
    {
        static bool Prefix()
        {
            if (BoardGameLogic.IsPlayingOtherPlayer && BoardGameLogic.IsChallenged) { return false; }

            PreplaceUnitsSeegaMessage message = new PreplaceUnitsSeegaMessage();
            MessageBroker.Instance.Publish(Agent.Main, message);

            return true;

        }
    }
}
