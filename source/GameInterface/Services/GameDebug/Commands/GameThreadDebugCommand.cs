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
    // coop.debug.gamethread.instrument [on|off|toggle|status]
    /// <summary>
    /// Turns the game-thread drain instrumentation on or off, or reports its current state. With no
    /// argument it flips the current setting.
    /// </summary>
    [CommandLineArgumentFunction("instrument", "coop.debug.gamethread")]
    public static string Instrument(List<string> args)
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
                return "Usage: coop.debug.gamethread.instrument [on|off|toggle|status]";
        }

        return $"GameThread drain instrumentation is {(GameThread.Instrument ? "ON" : "OFF")}. " +
               "When ON, a per-second [GameThread] summary (drain ms, worst frame, backlog, top handlers) is written to the log.";
    }

    [CommandLineArgumentFunction("stall", "coop.debug.gamethread")]
    public static string Stall(List<string> args)
    {
        if (ModInformation.IsClient)
        {
            return "gamethread.stall must be run on the server";
        }

        if (args.Count != 1 ||
            int.TryParse(args[0], out int milliseconds) == false ||
            milliseconds < 1 ||
            milliseconds > 5000)
        {
            return "Usage: coop.debug.gamethread.stall <milliseconds from 1 to 5000>";
        }

        Thread.Sleep(milliseconds);
        return $"Stalled the server game thread for {milliseconds} ms";
    }
}
