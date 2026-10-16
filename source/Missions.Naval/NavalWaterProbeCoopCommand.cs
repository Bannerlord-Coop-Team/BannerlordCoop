#if DEBUG
using Common.Commands;
using Newtonsoft.Json;
using System;
using System.Linq;
using TaleWorlds.MountAndBlade;

namespace Missions.Naval;

// coop.debug.naval.water_probe start grid=5 spacing=40 seconds=120
// coop.debug.naval.water_probe status
// coop.debug.naval.water_probe stop
/// <summary>
/// DEBUG: logs this client's water heights at a fixed grid and its hulls' heave, pitch and roll as NAVAL_WATER_PROBE lines
/// for an offline diff against another client's log. Read only; run it on every client of the battle.
/// </summary>
public sealed class NavalWaterProbeCoopCommand : ICoopCommand
{
    public string Prefix => "coop.debug.naval";

    public string Name => "water_probe";

    public string Description => "Logs NAVAL_WATER_PROBE lines (water height grid, clocks, hull heave/pitch/roll) on this client: " +
        "start [grid=5] [spacing=40] [seconds=120], stop or status.";

    public CoopCommandSide Side => CoopCommandSide.Client;

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("action", "start, stop or status.", true),
        new ExpectedArgs("knob_1", "grid=n, spacing=m or seconds=s.", false),
        new ExpectedArgs("knob_2", "grid=n, spacing=m or seconds=s.", false),
        new ExpectedArgs("knob_3", "grid=n, spacing=m or seconds=s.", false),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        var probe = Mission.Current?.GetMissionBehavior<NavalWaterProbeLogic>();
        if (probe == null || !CoopNavalMissionScope.IsActive) return Failed("No active coop naval battle mission.");

        var values = args?.ToArray() ?? Array.Empty<string>();
        if (values.Length == 0) return Failed(NavalWaterProbeSettings.Usage);

        switch (values[0].Trim().ToLowerInvariant())
        {
            case "start":
                var error = NavalWaterProbeSettings.TryParse(values.Skip(1), out var settings) ?? probe.Start(settings);
                return error != null ? Failed(error) : Status("started", probe);
            case "stop":
                if (values.Length > 1) return Failed(NavalWaterProbeSettings.Usage);
                probe.Stop("stopped");
                return Status("stopped", probe);
            case "status":
                if (values.Length > 1) return Failed(NavalWaterProbeSettings.Usage);
                return Status("status", probe);
            default:
                return Failed(NavalWaterProbeSettings.Usage);
        }
    }

    private static CoopCommandResult Status(string action, NavalWaterProbeLogic probe) =>
        new CoopCommandResult(true, "NAVAL_WATER_PROBE_STATUS " + JsonConvert.SerializeObject(new { action, probe = probe.Status() }));

    private static CoopCommandResult Failed(string output) => new CoopCommandResult(false, output, "command_failed");
}
#endif
