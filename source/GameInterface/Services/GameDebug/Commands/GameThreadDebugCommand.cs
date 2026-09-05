using Common.Commands;
using Common;
using System.Collections.Generic;
using System.Threading;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.GameDebug.Commands;

/// <summary>
/// Console commands for <see cref="GameThread"/> diagnostics. The optional instrumentation attributes
/// game-thread lag to marshaled handlers, while the server-only stall reproduces an authoritative
/// simulation hitch for synchronization testing.
/// </summary>
public class GameThreadDebugCommand
{
    private static CoopCommandResult Succeeded(string output) =>
        new CoopCommandResult(true, output);

    private static CoopCommandResult Failed(string output) =>
        new CoopCommandResult(false, output, "command_failed");

    // coop.debug.game_thread.instrument [on|off|toggle|status]
    /// <summary>
    /// Turns the game-thread drain instrumentation on or off, or reports its current state. With no
    /// argument it flips the current setting.
    /// </summary>
    public sealed class GameThreadInstrumentCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.game_thread";

        public string Name => "instrument";

        public string Description => "Runs the instrument debug operation.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("mode", "on, off, toggle, or status.", isRequired: false),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            var arg = args.Count > 0 ? args[0].ToLowerInvariant() : "toggle";

            switch (arg)
            {
                case "on":
                case "true":
                case "1":
                    GameThread.Instrument = true;
                    break;
                case "off":
                case "false":
                case "0":
                    GameThread.Instrument = false;
                    break;
                case "toggle":
                    GameThread.Instrument = !GameThread.Instrument;
                    break;
                case "status":
                    break;
                default:
                    return Failed($"Invalid mode '{args[0]}'. Expected on, off, toggle, or status.");
            }

            return Succeeded($"GameThread drain instrumentation is {(GameThread.Instrument ? "ON" : "OFF")}. " +
                   "When ON, a per-second [GameThread] summary (drain ms, worst frame, backlog, top handlers) is written to the log.");
        }
    }

    public sealed class GameThreadStallCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.game_thread";

        public string Name => "stall";

        public string Description => "Runs the stall debug operation.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("milliseconds", "The stall duration from 1 through 5000 milliseconds.", isRequired: true),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsClient)
            {
                return Failed("gamethread.stall must be run on the server");
            }

            if (int.TryParse(args[0], out int milliseconds) == false ||
                milliseconds < 1 ||
                milliseconds > 5000)
            {
                return Failed("Stall duration must be an integer from 1 through 5000 milliseconds.");
            }

            Thread.Sleep(milliseconds);
            return Succeeded($"Stalled the server game thread for {milliseconds} ms");
        }
    }
}
