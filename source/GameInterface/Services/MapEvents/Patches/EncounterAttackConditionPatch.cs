using Common.Logging;
using GameInterface.Configuration;
using HarmonyLib;
using Helpers;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.Localization;

namespace GameInterface.Services.MapEvents.Patches;

/// <summary>
/// Enables the Attack button for players who are wounded if the config is enabled to allow wounded battle entry.
/// </summary>
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

        if (!Hero.MainHero.IsWounded)
        {
            return;
        }

        Logger.Debug("Player is wounded and will be allowed to fight still.");
        AllowWoundedAttack(args);
    }

    // Vanilla returns before writing a tooltip when there is no player map event (the menu after a battle), and coop
    // patches may still show the option, so the tooltip is replaced rather than edited.
    internal static void AllowWoundedAttack(MenuCallbackArgs args)
    {
        args.IsEnabled = true;
        args.Tooltip = new TextObject("You are wounded - but you can still fight.");
    }
}