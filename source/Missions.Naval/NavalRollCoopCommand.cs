#if DEBUG
using Common.Commands;
using Missions.Battles;
using Newtonsoft.Json;
using System;
using System.Globalization;
using System.Linq;
using TaleWorlds.MountAndBlade;

namespace Missions.Naval;

/// <summary>Arms or reports a named hull's read-only paired roll measurement on this client.</summary>
public sealed class NavalRollCoopCommand : ICoopCommand
{
    private readonly INavalRollProbe probe;
    internal const string Usage = "Usage: coop.debug.naval.roll start ship-id-prefix at-water-seconds [seconds], status or stop";

    /// <summary>Each command gets a transient sampler; the mission's copy probe owns it after a successful start.</summary>
    public NavalRollCoopCommand(INavalRollProbe probe) => this.probe = probe;
    public string Prefix => "coop.debug.naval";
    public string Name => "roll";
    public string Description => "Logs paired owner/copy roll samples over a shared-clock interval. " + Usage;
    public CoopCommandSide Side => CoopCommandSide.Client;
    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[] {
        new ExpectedArgs("action", "start, status or stop", true),
        new ExpectedArgs("ship", "Unambiguous ship id prefix from inspect summary", false),
        new ExpectedArgs("at", "Future shared water time in seconds (water_clock written / 10)", false),
        new ExpectedArgs("seconds", "Window length, 1 to 300 seconds; default 60", false),
    };

    /// <summary>Resolves hulls on the game thread and attaches only diagnostics, without changing authoritative game state.</summary>
    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        var values = args?.ToArray() ?? Array.Empty<string>();
        var mission = Mission.Current;
        var copyProbe = mission?.GetMissionBehavior<NavalCopyProbeLogic>();
        var controller = mission?.GetMissionBehavior<CoopBattleController>();
        if (copyProbe == null || controller == null || !CoopNavalMissionScope.IsActive)
            return new CoopCommandResult(false, "No active coop naval battle mission.", "command_failed");
        if (values.Length == 1 && (values[0] == "status" || values[0] == "stop"))
        {
            if (values[0] == "stop") copyProbe.RollProbe?.Stop("stopped");
            return Status(copyProbe);
        }
        if (values.Length < 3 || values.Length > 4 || values[0] != "start" || string.IsNullOrWhiteSpace(values[1])
            || !float.TryParse(values[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float at))
            return new CoopCommandResult(false, Usage, "command_failed");
        float seconds = 60f;
        if (values.Length == 4 && !float.TryParse(values[3], NumberStyles.Float, CultureInfo.InvariantCulture, out seconds))
            return new CoopCommandResult(false, Usage, "command_failed");
        if (!mission.IsDeploymentFinished)
            return new CoopCommandResult(false, "Wait until deployment has finished.", "command_failed");
        if (copyProbe.RollProbe?.IsRunning == true)
            return new CoopCommandResult(false, "A roll probe is already running; stop it first.", "command_failed");
        var ships = controller.MissionComponent.ShipRegistry.Ships
            .Where(ship => ship.ShipId.ToString().StartsWith(values[1], StringComparison.OrdinalIgnoreCase)).ToArray();
        if (ships.Length != 1) return new CoopCommandResult(false, "Ship prefix must match exactly one registered hull.", "command_failed");
        string error = probe.Start(ships[0], controller.Session.InstanceId, controller.Session.OwnControllerId, at, seconds);
        if (error != null) return new CoopCommandResult(false, error, "command_failed");
        copyProbe.RollProbe = probe;
        return Status(copyProbe);
    }

    /// <summary>Reports the mission-owned sampler even when this command instance was resolved later.</summary>
    private CoopCommandResult Status(NavalCopyProbeLogic copyProbe) => new CoopCommandResult(true,
        "NAVAL_ROLL_STATUS " + JsonConvert.SerializeObject(copyProbe.RollProbe?.Status() ?? new { reason = "idle" }));
}
#endif
