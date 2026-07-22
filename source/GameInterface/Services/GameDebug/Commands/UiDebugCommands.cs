using Common.Logging;
using GameInterface.Services.UI.CoopOptions;
using GameInterface.Utils.Commands;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade.View;
using TaleWorlds.MountAndBlade.ViewModelCollection.GameOptions;
using TaleWorlds.ScreenSystem;
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

    private const string OpenCoopOptionsUsage = "Usage: coop.debug.ui.open_coop_options";
    private const string OpenPerformanceOptionsUsage = "Usage: coop.debug.ui.open_performance_options";
    private const string CloseTopScreenUsage = "Usage: coop.debug.ui.close_top_screen";

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

    [CommandLineArgumentFunction("open_coop_options", "coop.debug.ui")]
    public static string OpenCoopOptions(List<string> args)
    {
        if (args.Count != 0) return OpenCoopOptionsUsage;

        try
        {
            ScreenManager.PushScreen(ViewCreatorManager.CreateScreenView<CoopOptionsUI>());
        }
        catch (Exception ex)
        {
            return CommandHelpers.FormatException("Open coop options", ex);
        }

        return "Opened coop options.";
    }

    [CommandLineArgumentFunction("open_performance_options", "coop.debug.ui")]
    public static string OpenPerformanceOptions(List<string> args)
    {
        if (args.Count != 0) return OpenPerformanceOptionsUsage;

        ScreenBase screen = null;
        OptionsVM options = null;

        try
        {
            screen = ViewCreator.CreateOptionsScreen(fromMainMenu: false);

            // TaleWorlds.MountAndBlade.GauntletUI is not publicized, so its private data source is read here.
            var field = screen.GetType().GetField("_dataSource", BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
                return "Failed: vanilla options data source was unavailable.";

            ScreenManager.PushScreen(screen);

            options = field.GetValue(screen) as OptionsVM;
            if (options == null)
            {
                CloseVanillaOptionsScreen(screen, options);
                return "Failed: vanilla options data source was unavailable.";
            }

            int performanceIndex = options.GetIndexOfCategory(options.PerformanceOptions);
            for (int i = 0; i < 6 && options.CategoryIndex != performanceIndex; i++)
                options.SelectNextCategory();
            if (options.CategoryIndex != performanceIndex)
            {
                CloseVanillaOptionsScreen(screen, options);
                return "Failed: vanilla Performance options tab was unavailable.";
            }
        }
        catch (Exception ex)
        {
            CloseVanillaOptionsScreen(screen, options);
            return CommandHelpers.FormatException("Open Performance options", ex);
        }

        return "Opened vanilla Performance options.";
    }

    [CommandLineArgumentFunction("close_top_screen", "coop.debug.ui")]
    public static string CloseTopScreen(List<string> args)
    {
        if (args.Count != 0) return CloseTopScreenUsage;
        if (ScreenManager.TopScreen == null) return "Failed: no screen is open.";

        try
        {
            var screen = ScreenManager.TopScreen;
            if (TryGetVanillaOptionsDataSource(screen, out var options))
                options.ExecuteCancel();
            else
                ScreenManager.PopScreen();
        }
        catch (Exception ex)
        {
            return CommandHelpers.FormatException("Close top screen", ex);
        }

        return "Closed the top screen.";
    }

    private static bool TryGetVanillaOptionsDataSource(ScreenBase screen, out OptionsVM options)
    {
        var field = screen?.GetType().GetField("_dataSource", BindingFlags.Instance | BindingFlags.NonPublic);
        options = field?.GetValue(screen) as OptionsVM;
        return options != null;
    }

    private static void CloseVanillaOptionsScreen(ScreenBase screen, OptionsVM options)
    {
        if (screen == null || !ReferenceEquals(ScreenManager.TopScreen, screen)) return;

        if (options == null)
            TryGetVanillaOptionsDataSource(screen, out options);

        if (options != null)
        {
            options.ExecuteCancel();
            return;
        }

        ScreenManager.PopScreen();
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
