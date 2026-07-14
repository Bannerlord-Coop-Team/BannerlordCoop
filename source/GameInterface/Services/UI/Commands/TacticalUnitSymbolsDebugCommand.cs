using Autofac;
using GameInterface.Services.UI.Handlers;
using GameInterface.Utils.Commands;
using System.Collections.Generic;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.UI.Commands;

public class TacticalUnitSymbolsDebugCommand
{
    private const string CommandName = "coop.debug.ui.tactical_symbols";
    private const string Usage = "Usage: coop.debug.ui.tactical_symbols <on|off|toggle|status>";

    [CommandLineArgumentFunction("tactical_symbols", "coop.debug.ui")]
    public static string TacticalSymbols(List<string> args)
    {
        if (!CommandHelpers.IsServerOnlyCommand(out var error, CommandName)) return error;
        if (args.Count != 1) return Usage;

        switch (args[0].ToLowerInvariant())
        {
            case "on":
            case "true":
            case "1":
                return Apply(true);
            case "off":
            case "false":
            case "0":
                return Apply(false);
            case "toggle":
                return Apply(!TacticalUnitSymbolsSettings.HideTacticalUnitSymbols);
            case "status":
                return StatusText;
            default:
                return Usage;
        }
    }

    private static string Apply(bool hideTacticalUnitSymbols)
    {
        if (!ContainerProvider.TryResolve<TacticalUnitSymbolsConfigHandler>(out var handler))
            return "Tactical unit symbols configuration is unavailable.";

        handler.SetAndBroadcast(hideTacticalUnitSymbols);
        return StatusText;
    }

    private static string StatusText =>
        $"Tactical unit symbols are {(TacticalUnitSymbolsSettings.HideTacticalUnitSymbols ? "hidden" : "visible")}.";
}
