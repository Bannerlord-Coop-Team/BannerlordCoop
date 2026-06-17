using Common;
using System.Collections.Generic;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.GameDebug.Commands;

/// <summary>
/// Console commands for the <see cref="GameThread"/> drain instrumentation. The instrumentation times
/// how long the game thread spends applying marshaled network actions each frame and logs a per-second
/// summary (drain time, worst single-frame hitch, backlog depth, and the handlers that dominate the
/// cost), which attributes game-thread/render lag to the handlers that cause it. It is off by default
/// and purely local — run the command on the process you want to profile (the client, to diagnose
/// client-side lag).
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
}
