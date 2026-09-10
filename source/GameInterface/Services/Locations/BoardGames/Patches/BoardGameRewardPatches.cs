using Common;
using Common.Messaging;
using GameInterface.Services.Locations.BoardGames.Messages;
using HarmonyLib;
using Helpers;
using SandBox.CampaignBehaviors;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Locations.BoardGames.Patches;

[HarmonyPatch(typeof(BoardGameCampaignBehavior), "OnPlayerBoardGameOver")]
internal static class BoardGameRewardPatches
{
    // Prefix runs before vanilla's rolls. Resetting the sticky flags lets the
    // postfix attribute exactly this game's outcome to this game.
    internal static void Prefix(BoardGameCampaignBehavior __instance, out int __state)
    {
        __state = 0;
        if (ModInformation.IsServer) return;
        // Captured here: vanilla ends the call with SetBetAmount(0), so
        // the postfix would otherwise always see a zero bet and forward nothing.
        __state = __instance._betAmount;
        __instance._relationGained = false;
        __instance._influenceGained = false;
        __instance._renownGained = false;
        __instance._gainedNothing = false;
    }

    internal static void Postfix(BoardGameCampaignBehavior __instance, Hero opposingHero, BoardGameHelper.BoardGameState state, int __state)
    {
        if (ModInformation.IsServer) return;

        if (opposingHero == null)
        {
            // Tavern game. Bet 0, draw, and cancel need no forwarding.
            if (__state <= 0) return;
            if (state != BoardGameHelper.BoardGameState.Win && state != BoardGameHelper.BoardGameState.Loss) return;
            MessageBroker.Instance.Publish(__instance, new BoardGameTavernResult(
                __state,
                state));
            return;
        }

        // Lord game
        if (state != BoardGameHelper.BoardGameState.Win) return;
        var reward = LordBoardGameReward.None;
        if (__instance._relationGained) reward = LordBoardGameReward.Relation;
        else if (__instance._influenceGained) reward = LordBoardGameReward.Influence;
        else if (__instance._renownGained) reward = LordBoardGameReward.Renown;
        MessageBroker.Instance.Publish(__instance, new BoardGameLordResult(
            opposingHero,
            __instance._difficulty,
            reward,
            __instance._opposingHeroExtraXPGained));
    }
}
