using Common.Logging;
using GameInterface.Utils.Commands;
using Serilog;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.Core;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.GameDebug.Commands;

/// <summary>
/// [Debug] UI / screen commands. <c>coop.debug.ui.close_screen</c> forces the current game menu to exit
/// (<see cref="GameMenu.ExitToLast"/>) — a manual escape for when a post-battle encounter screen is left open.
/// </summary>
internal class UiDebugCommands
{
    public static readonly ILogger Logger = LogManager.GetLogger<UiDebugCommands>();

    private const string CloseScreenUsage =
@"Usage:
  coop.debug.ui.close_screen

Exits the current game menu (GameMenu.ExitToLast). Use to dismiss a post-battle encounter screen left open.";

    [CommandLineArgumentFunction("close_screen", "coop.debug.ui")]
    public static string CloseScreen(List<string> args)
    {
        var ctx = new CommandContext("close_screen", CloseScreenUsage, args);
        if (!ctx.RequireArgCount(0, out var error))
            return error;

        if (Campaign.Current == null)
            return "Failed: no active campaign.";

        try
        {
            GameMenu.ExitToLast();
        }
        catch (Exception ex)
        {
            return CommandHelpers.FormatException("Close screen", ex);
        }

        return "Called GameMenu.ExitToLast().";
    }

    [CommandLineArgumentFunction("pop_state", "coop.debug.ui")]
    public static string PopState(List<string> args)
    {
        if (args.Count != 0)
            return "Usage: coop.debug.ui.pop_state";

        TaleWorlds.Core.GameState activeState = Game.Current?.GameStateManager?.ActiveState;
        if (activeState == null)
            return "Failed: no active game state.";

        if (activeState is MapState)
            return "Active state is already MapState.";

        Game.Current.GameStateManager.PopState();
        return $"Queued pop for {activeState.GetType().Name}.";
    }

    [CommandLineArgumentFunction("active_state", "coop.debug.ui")]
    public static string ActiveState(List<string> args)
    {
        if (args.Count != 0)
            return "Usage: coop.debug.ui.active_state";

        return Game.Current?.GameStateManager?.ActiveState?.GetType().Name ?? "none";
    }
}
