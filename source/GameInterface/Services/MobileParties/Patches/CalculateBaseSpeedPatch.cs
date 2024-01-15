using GameInterface.Services.MobileParties.Extensions;
using HarmonyLib;
using System;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace GameInterface.Services.MobileParties.Patches;

[HarmonyPatch(typeof(DefaultPartySpeedCalculatingModel))]
internal class CalculateBaseSpeedPatch 
{
    [HarmonyPatch(nameof(DefaultPartySpeedCalculatingModel.CalculateBaseSpeed))]
    [HarmonyPostfix]
    private static void CalculateBaseSpeed(ref MobileParty mobileParty, ref ExplainedNumber __result)
    {
        if(ModInformation.IsServer && mobileParty.IsPartyControlled() == false)
        {
            float playerMapMovementSpeedBonusMultiplier = Campaign.Current.Models.DifficultyModel.GetPlayerMapMovementSpeedBonusMultiplier();
            if (playerMapMovementSpeedBonusMultiplier > 0f)
            {
                __result.AddFactor(playerMapMovementSpeedBonusMultiplier, GameTexts.FindText("str_game_difficulty"));
            }
        }
    }
}

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
