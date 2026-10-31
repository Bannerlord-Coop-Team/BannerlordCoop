#if DEBUG
using Common.Commands;
using Missions.Battles;
using Newtonsoft.Json;
using System;
using System.Globalization;
using System.Linq;
using TaleWorlds.MountAndBlade;

namespace Missions.Naval;

/// <summary>Controls a mission-owned, read-only ballista probe on any participating client.</summary>
public sealed class NavalWeaponCoopCommand : ICoopCommand
{
    private readonly INavalWeaponProbe probe;
    private const string Usage = "Usage: coop.debug.naval.weapon_probe start at-water-seconds [seconds], status or stop";

    /// <summary>Resolves a transient probe only used when the mission has no existing probe.</summary>
    public NavalWeaponCoopCommand(INavalWeaponProbe probe) => this.probe = probe;
    public string Prefix => "coop.debug.naval";
    public string Name => "weapon_probe";
    public string Description => "Logs shared-clock weapon samples and animation events. " + Usage;
    public CoopCommandSide Side => CoopCommandSide.Client;
    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[] {
        new ExpectedArgs("action", "start, status or stop", true),
        new ExpectedArgs("at", "Future shared water seconds", false),
        new ExpectedArgs("seconds", "Duration 1 to 300, default 60", false),
    };

    /// <summary>Attaches one sampler to the mission and reuses it for subsequent commands.</summary>
    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        var values = args?.ToArray() ?? Array.Empty<string>();
        var mission = Mission.Current;
        var controller = mission?.GetMissionBehavior<CoopBattleController>();
        if (controller == null || !CoopNavalMissionScope.IsActive) return new CoopCommandResult(false, "No active coop naval battle mission.", "command_failed");
        INavalWeaponProbe active = mission.GetMissionBehavior<NavalWeaponProbe>();
        if (values.Length == 1 && (values[0] == "status" || values[0] == "stop"))
        {
            if (values[0] == "stop") active?.Stop("stopped");
            return new CoopCommandResult(true, "NAVAL_WEAPON_STATUS " + JsonConvert.SerializeObject(active?.Status() ?? new { reason = "idle" }));
        }
        if (values.Length < 2 || values.Length > 3 || values[0] != "start" ||
            !float.TryParse(values[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float at))
            return new CoopCommandResult(false, Usage, "command_failed");
        float seconds = 60f;
        if (values.Length == 3 && !float.TryParse(values[2], NumberStyles.Float, CultureInfo.InvariantCulture, out seconds))
            return new CoopCommandResult(false, Usage, "command_failed");
        if (!mission.IsDeploymentFinished) return new CoopCommandResult(false, "Wait until deployment has finished.", "command_failed");
        var selected = active ?? probe;
        string error = selected.Start(controller, at, seconds);
        if (error != null) return new CoopCommandResult(false, error, "command_failed");
        if (active == null) mission.AddMissionBehavior((NavalWeaponProbe)selected);
        return new CoopCommandResult(true, "NAVAL_WEAPON_STATUS " + JsonConvert.SerializeObject(selected.Status()));
    }
}
#endif
