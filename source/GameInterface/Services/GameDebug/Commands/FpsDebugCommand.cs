using Common;
using System.Collections.Generic;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.GameDebug.Commands;

/// <summary>
/// Console command for <see cref="FpsLogger"/> diagnostics. The opt-in toggle records a periodic
/// per-second FPS summary to the log on the client or server being profiled.
/// </summary>
public class FpsDebugCommand
{
    // coop.debug.fps.log [on|off|toggle|status]
    /// <summary>
    /// Turns the FPS logger on or off, or reports its current state. With no argument it flips
    /// the current setting.
    /// </summary>
    [CommandLineArgumentFunction("log", "coop.debug.fps")]
    public static string Fps(List<string> args)
    {
        var arg = args.Count > 0 ? args[0].ToLowerInvariant() : "toggle";

        switch (arg)
        {
            case "on":
            case "true":
            case "1":
                FpsLogger.Enabled = true;
                break;
            case "off":
            case "false":
            case "0":
                FpsLogger.Enabled = false;
                break;
            case "toggle":
                FpsLogger.Enabled = !FpsLogger.Enabled;
                break;
            case "status":
                break;
            default:
                return "Usage: coop.debug.fps.log [on|off|toggle|status]";
        }

        return $"FPS logging is {(FpsLogger.Enabled ? "ON" : "OFF")}. " +
               "When ON, a per-second [Fps] summary (avg/min/max) is written to the log.";
    }
}
