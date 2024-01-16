using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using System;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.MobileParties.Patches;

[HarmonyPatch(typeof(DefaultPartySpeedCalculatingModel))]
internal class CalculateBaseSpeedPatch 
{
    [HarmonyPatch(nameof(DefaultPartySpeedCalculatingModel.CalculateBaseSpeed))]
    [HarmonyPostfix]
    private static void CalculateBaseSpeed(ref MobileParty mobileParty, ref ExplainedNumber __result)
    {
        if(mobileParty != MobileParty.MainParty && mobileParty.IsPlayerParty())
        {
            
            float playerMapMovementSpeedBonusMultiplier = Campaign.Current.Models.DifficultyModel.GetPlayerMapMovementSpeedBonusMultiplier();
            if (playerMapMovementSpeedBonusMultiplier > 0f)
            {
                __result.AddFactor(playerMapMovementSpeedBonusMultiplier, GameTexts.FindText("str_game_difficulty"));
            }
        }
    }
}

/// <summary>
/// Fixes issue with DefaultPartySpeedCalculatingModel._culture statically calls GameTexts.FindText
/// and when harmony patches, it calls the static constructor for DefaultPartySpeedCalculatingModel
/// and results in a null reference exception because _gameTextManager has not been initialized
/// This patch initializes _gameTextManager if it is null
/// </summary>
[HarmonyPatchCategory(GameInterface.HARMONY_STATIC_FIXES_CATEGORY)]
[HarmonyPatch(typeof(GameTexts))]
internal class GameTextsPatches
{
    private static readonly FieldInfo get_GameTextManager = typeof(GameTexts)
        .GetField("_gameTextManager", BindingFlags.Static | BindingFlags.NonPublic);

    [HarmonyPatch(nameof(GameTexts.FindText))]
    [HarmonyPrefix]
    private static void FindTextPrefix()
    {
        if (get_GameTextManager.GetValue(null) == null)
        {
            var gameTextManager = new GameTextManager();
            gameTextManager.LoadGameTexts();
            GameTexts.Initialize(gameTextManager);
        }
    }
}
