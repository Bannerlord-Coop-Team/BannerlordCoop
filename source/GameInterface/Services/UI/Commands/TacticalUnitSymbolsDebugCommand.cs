using Common.Commands;
using Autofac;
using GameInterface.Services.UI.Handlers;
using GameInterface.Utils.Commands;
using System.Collections.Generic;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.UI.Commands;

public class TacticalUnitSymbolsDebugCommand
{
    private static CoopCommandResult Succeeded(string output) =>
        new CoopCommandResult(true, output);

    private static CoopCommandResult Failed(string output) =>
        new CoopCommandResult(false, output, "command_failed");

    private const string CommandName = "coop.debug.ui.tactical_symbols";
    public sealed class UiTacticalSymbolsCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.ui";

        public string Name => "tactical_symbols";

        public string Description => "Runs the tactical symbols debug operation.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("mode", "on, off, toggle, or status.", isRequired: true),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!CommandHelpers.IsServerOnlyCommand(out var error, CommandName)) return Failed(error);

            switch (args[0].ToLowerInvariant())
            {
                case "on":
                case "true":
                case "1":
                    return Apply(false);
                case "off":
                case "false":
                case "0":
                    return Apply(true);
                case "toggle":
                    return Apply(!TacticalUnitSymbolsSettings.HideTacticalUnitSymbols);
                case "status":
                    return Succeeded(StatusText);
                default:
                    return Failed($"Invalid mode '{args[0]}'. Expected on, off, toggle, or status.");
            }
        }
    }

    private static CoopCommandResult Apply(bool hideTacticalUnitSymbols)
    {
        if (!ContainerProvider.TryResolve<TacticalUnitSymbolsConfigHandler>(out var handler))
            return Failed("Tactical unit symbols configuration is unavailable.");

        handler.SetAndBroadcast(hideTacticalUnitSymbols);
        return Succeeded(StatusText);
    }

    private static string StatusText =>
        $"Tactical unit symbols are {(TacticalUnitSymbolsSettings.HideTacticalUnitSymbols ? "hidden" : "visible")}.";
}
