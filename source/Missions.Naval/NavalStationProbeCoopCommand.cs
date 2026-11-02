#if DEBUG
using Common.Commands;
using System.Collections.Generic;
using System.Globalization;

namespace Missions.Naval;

/// <summary>Exposes the DEBUG station diagnostic through the ordinary client command dispatcher.</summary>
public sealed class NavalStationProbeCoopCommand : ICoopCommand
{
    internal const string Usage = "Usage: coop.debug.naval.station_probe snapshot [hullPrefix] | watch <hullPrefix> <seconds (1-300)> | reseat <hullPrefix> <agentIndex>";
    private readonly INavalStationProbe probe;

    /// <summary>Uses the injected mission diagnostic rather than global probe state.</summary>
    public NavalStationProbeCoopCommand(INavalStationProbe probe) => this.probe = probe;

    public string Prefix => "coop.debug.naval";
    public string Name => "station_probe";
    public string Description => "Snapshots station users, watches every fifth frame, or tests an owner-side re-seat.";
    public CoopCommandSide Side => CoopCommandSide.Client;
    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("mode", "snapshot, watch or reseat.", true),
        new ExpectedArgs("hullPrefix", "Hull id prefix from inspect; optional for snapshot.", false),
        new ExpectedArgs("value", "Watch duration in seconds (1-300), or local agent index.", false),
    };

    /// <summary>Rejects malformed input before accessing native mission state.</summary>
    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        if (!TryParse(args, out var mode, out var prefix, out int value))
            return new CoopCommandResult(false, Usage, "command_failed");
        return probe.Execute(mode, prefix, value);
    }

    /// <summary>Accepts only complete modes and bounded, invariant integer arguments.</summary>
    internal static bool TryParse(IReadOnlyList<string> args, out string mode, out string prefix, out int value)
    {
        mode = null;
        prefix = null;
        value = 0;
        if (args == null || args.Count < 1 || args.Count > 3 || string.IsNullOrWhiteSpace(args[0])) return false;
        mode = args[0].Trim().ToLowerInvariant();
        if (args.Count > 1)
        {
            if (string.IsNullOrWhiteSpace(args[1])) return false;
            prefix = args[1].Trim();
        }
        if (mode == "snapshot") return args.Count <= 2;
        if (mode != "watch" && mode != "reseat") return false;
        if (args.Count != 3 || !int.TryParse(args[2], NumberStyles.None, CultureInfo.InvariantCulture, out value)) return false;
        return mode == "reseat" || (value >= 1 && value <= 300);
    }
}
#endif
