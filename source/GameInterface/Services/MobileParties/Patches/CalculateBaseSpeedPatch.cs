using Common.Logging;
using GameInterface.Services.MobileParties.Extensions;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using GameInterface.Policies;

namespace GameInterface.Services.MobileParties.Patches;


/// <summary>
/// Applies speed difficulty modifier to all player parties on client & server.
/// </summary>
[HarmonyPatch(typeof(DefaultPartySpeedCalculatingModel))]
internal class CalculateBaseSpeedPatch 
{
    [HarmonyPatch(nameof(DefaultPartySpeedCalculatingModel.CalculateBaseSpeed))]
    [HarmonyPostfix]
    private static void CalculateBaseSpeed(ref MobileParty mobileParty, ref ExplainedNumber __result)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return;
        if(mobileParty.IsPlayerParty() && mobileParty != MobileParty.MainParty)
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

    [HarmonyPatch(nameof(GameTexts.FindText))]
    [HarmonyPrefix]
    private static void FindTextPrefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return;
        if (GameTexts._gameTextManager == null)
        {
            var gameTextManager = new GameTextManager();
            gameTextManager.LoadGameTexts();
            GameTexts.Initialize(gameTextManager);
        }
    }
}

/// <summary>
/// Guards against NullReferenceException in <see cref="DefaultPartyMoraleModel.GetEffectivePartyMorale"/>
/// when a Militia party's <see cref="TaleWorlds.CampaignSystem.Party.MobileParty.HomeSettlement"/> is null
/// during a multiplayer sync transition on the client.
/// </summary>
[HarmonyPatch(typeof(DefaultPartyMoraleModel), nameof(DefaultPartyMoraleModel.GetEffectivePartyMorale))]
internal class PartyMoraleModelRobustnessPatch
{
    private static readonly ILogger Logger = LogManager.GetLogger<PartyMoraleModelRobustnessPatch>();

    [HarmonyPrefix]
    private static bool Prefix(MobileParty mobileParty, bool includeDescription, ref ExplainedNumber __result)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        if (mobileParty.IsMilitia && mobileParty.HomeSettlement == null)
        {
            Logger.Debug("DefaultPartyMoraleModel.GetEffectivePartyMorale: skipping militia party {Party} with null HomeSettlement",
                mobileParty.StringId ?? "null");
            __result = new ExplainedNumber(50f, includeDescription, null);
            return false;
        }
        return true;
    }
}
