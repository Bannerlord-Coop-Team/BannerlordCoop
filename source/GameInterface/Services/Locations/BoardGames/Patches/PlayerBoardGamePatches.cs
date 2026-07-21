using GameInterface.Services.Locations.BoardGames;
using HarmonyLib;
using SandBox.BoardGames;
using SandBox.BoardGames.MissionLogics;
using SandBox.BoardGames.Pawns;
using SandBox.CampaignBehaviors;
using SandBox.Conversation;
using TaleWorlds.CampaignSystem;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.MissionViews;

namespace GameInterface.Services.Locations.BoardGames.Patches;

[HarmonyPatch]
internal static class PlayerBoardGamePatches
{
    [HarmonyPatch(typeof(BoardGameCampaignBehavior), "conversation_lord_talk_game_on_condition")]
    [HarmonyPostfix]
    private static void LordTalkGameConditionPostfix(BoardGameCampaignBehavior __instance, ref bool __result)
    {
        if (__result || !IsTavernBoardGameOpponent(
                CharacterObject.OneToOneConversationCharacter?.Occupation,
                CampaignMission.Current?.Location?.StringId) ||
            !MissionBoardGameLogic.IsBoardGameAvailable()) return;

        __instance.InitializeConversationVars();
        __result = true;
    }

    internal static bool IsTavernBoardGameOpponent(Occupation? occupation, string locationId)
        => occupation == Occupation.Lord && locationId == "tavern";

    [HarmonyPatch(typeof(MissionBoardGameLogic), nameof(MissionBoardGameLogic.DetectOpposingAgent))]
    [HarmonyPrefix]
    private static bool DetectOpposingAgentPrefix(MissionBoardGameLogic __instance)
        => !PlayerBoardGameCoordinator.TryRequestGame(__instance, ConversationMission.OneToOneConversationAgent);

    [HarmonyPatch(typeof(BoardGameBase), "UpdateTurn")]
    [HarmonyPrefix]
    private static bool UpdateTurnPrefix(BoardGameBase __instance)
        => !PlayerBoardGameCoordinator.ShouldSuppressAiTurn(__instance);

    [HarmonyPatch(typeof(BoardGameBase), "HandlePlayerInput")]
    [HarmonyPostfix]
    private static void HandlePlayerInputPostfix(BoardGameBase __instance, Move __result)
        => PlayerBoardGameCoordinator.TrySendMove(__instance, __result);

    [HarmonyPatch(typeof(BoardGameBase), nameof(BoardGameBase.SetPawnCaptured))]
    [HarmonyPostfix]
    private static void SetPawnCapturedPostfix(BoardGameBase __instance, PawnBase pawn, bool fake)
        => PlayerBoardGameCoordinator.TrySendCapturedPawn(__instance, pawn, fake);

    [HarmonyPatch(typeof(MissionBoardGameLogic), nameof(MissionBoardGameLogic.SetGameOver))]
    [HarmonyPrefix]
    private static bool SetGameOverPrefix(MissionBoardGameLogic __instance, GameOverEnum gameOverInfo)
        => !PlayerBoardGameCoordinator.TryCompleteGame(__instance, gameOverInfo);

    [HarmonyPatch(typeof(MissionBoardGameLogic), "StartConversationWithOpponentAfterGameEnd")]
    [HarmonyPrefix]
    private static bool StartConversationWithOpponentAfterGameEndPrefix(MissionBoardGameLogic __instance)
        => PlayerBoardGameCoordinator.StartConversationAfterGameEnd(__instance);

    [HarmonyPatch(typeof(MissionMainAgentController), nameof(MissionMainAgentController.OnPreMissionTick))]
    [HarmonyPostfix]
    private static void MainAgentControlPostfix()
        => PlayerBoardGameCoordinator.ReassertLocationPlayerControl();

    [HarmonyPatch(typeof(MissionBoardGameLogic), "get_IsOpposingAgentMovingToPlayingChair")]
    [HarmonyPrefix]
    private static bool IsOpposingAgentMovingToPlayingChairPrefix(MissionBoardGameLogic __instance, ref bool __result)
    {
        if (!PlayerBoardGameCoordinator.IsPlayerGame(__instance)) return true;

        __result = false;
        return false;
    }
}
