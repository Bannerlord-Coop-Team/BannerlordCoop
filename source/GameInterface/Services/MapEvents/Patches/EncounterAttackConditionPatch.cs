using Common.Logging;
using GameInterface.Configuration;
using HarmonyLib;
using Helpers;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;

namespace GameInterface.Services.MapEvents.Patches;

[HarmonyPatch(typeof(MenuHelper))]
public class EncounterAttackConditionPatch
{
    private static readonly ILogger Logger = LogManager.GetLogger<EncounterAttackConditionPatch>();

    /// <summary>
    /// Enables the attack button for players who are wounded still.
    /// </summary>
    /// <param name="args">MenuCallbackArgs</param>
    [HarmonyPatch(nameof(MenuHelper.EncounterAttackCondition))]
    [HarmonyPostfix]
    internal static void EncounterAttackConditionPostfix(MenuCallbackArgs args)
    {
        if (!ModConfigProvider.ModOptions.PlayerWoundedBattleEntry)
        {
            return;
        }
        
        if (!Hero.MainHero.IsHumanPlayerCharacter)
        {
            return;
        }

        if (!Hero.MainHero.IsWounded)
        {
            return;
        }

        Logger.Debug("Player is wounded and will be allowed to fight still.");
        args.IsEnabled = true;
        args.Tooltip.Value = "You are wounded - but you can still fight.";
    }
}