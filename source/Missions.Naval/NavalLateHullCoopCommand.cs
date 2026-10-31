#if DEBUG
using Common.Commands;
using System.Collections.Generic;
using System.Globalization;

namespace Missions.Naval;

/// <summary>Stages and observes the late-deployment station checks on the client simulating the chosen agent.</summary>
public interface INavalLateHullCoopCommand : ICoopCommand { }

/// <inheritdoc cref="INavalLateHullCoopCommand"/>
public sealed class NavalLateHullCoopCommand : INavalLateHullCoopCommand
{
    internal const string Usage = "Usage: coop.debug.naval.late_hull [shipId-prefix [climb|rope|oar|release <agent-index> [station-index]]]";
    private readonly INavalLateHullProbe probe;

    /// <summary>Uses the local mission probe for state reads and authoritative station actions.</summary>
    public NavalLateHullCoopCommand(INavalLateHullProbe probe) => this.probe = probe;

    public string Prefix => "coop.debug.naval";
    public string Name => "late_hull";
    public string Description => "Lists stations, agents and ballista targets, or stages a climb, AI rope/oar use or release.";
    public CoopCommandSide Side => CoopCommandSide.Client;
    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("shipId-prefix", "Full hull id or unique prefix from inspect; omit to inspect all hulls and agents.", false),
        new ExpectedArgs("operation", "climb, rope, oar or release; omit to inspect.", false),
        new ExpectedArgs("agent-index", "Local agent index from late_hull output, on its authority client.", false),
        new ExpectedArgs("station-index", "Index in the chosen kind's station list; defaults to zero.", false),
    };

    /// <summary>Rejects malformed arguments before reading the mission or changing a station.</summary>
    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        if (!TryParse(args, out var ship, out var operation, out int agent, out int station))
            return new CoopCommandResult(false, Usage, "command_failed");

        return probe.Execute(ship, operation, agent, station);
    }

    /// <summary>Reads an inspection or one station action, keeping release free of a meaningless station argument.</summary>
    internal static bool TryParse(IReadOnlyList<string> args, out string ship, out string operation, out int agent, out int station)
    {
        ship = null;
        operation = null;
        agent = -1;
        station = 0;
        if (args == null || args.Count == 0) return true;
        if (args.Count > 4 || string.IsNullOrWhiteSpace(args[0])) return false;
        ship = args[0].Trim();
        if (args.Count == 1) return true;
        if (args.Count < 3) return false;

        operation = args[1].ToLowerInvariant();
        if (operation != "climb" && operation != "rope" && operation != "oar" && operation != "release") return false;
        if (!int.TryParse(args[2], NumberStyles.None, CultureInfo.InvariantCulture, out agent)) return false;
        if (args.Count == 3) return true;
        return operation != "release" && int.TryParse(args[3], NumberStyles.None, CultureInfo.InvariantCulture, out station);
    }
}
#endif
