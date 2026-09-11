#if DEBUG
using Common.Commands;
using Newtonsoft.Json;
using System;
using System.Globalization;

namespace Missions.Battles;

public interface INavalDriftAdapter
{
    object StartDrift(Guid operationId, int seconds);
    object InspectDrift();
}

public sealed class NavalLabDriftStartCommand : ICoopCommand
{
    private readonly INavalLabCoordinator coordinator;
    public NavalLabDriftStartCommand(INavalLabCoordinator coordinator) => this.coordinator = coordinator;
    public string Prefix => "coop.debug.naval_lab";
    public string Name => "drift-start";
    public string Description => "Start one read-only 1-60 second station drift recording on this client. No input or station-cache writes.";
    public CoopCommandSide Side => CoopCommandSide.Client;
    public IExpectedArgs[] ExpectedArgs { get; } =
    {
        new ExpectedArgs("operation_id", "One recording UUID; duplicates preserve the original recording.", true),
        new ExpectedArgs("seconds", "Integer 1-60; use 30 for the bounded drift experiment.", true)
    };
    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        if (!Guid.TryParse(args[0], out var id) || id == Guid.Empty
            || !int.TryParse(args[1], NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) || seconds < 1 || seconds > 60)
            return new CoopCommandResult(false, "Expected nonempty UUID and seconds 1-60.", "invalid_arguments");
        try { return new CoopCommandResult(true, "LIVE_TEST_JSON=" + JsonConvert.SerializeObject(coordinator.StartDrift(id, seconds))); }
        catch (Exception exception) { return new CoopCommandResult(false, exception.Message, "drift_rejected"); }
    }
}

public sealed class NavalLabDriftStatusCommand : ICoopCommand
{
    private readonly INavalLabCoordinator coordinator;
    public NavalLabDriftStatusCommand(INavalLabCoordinator coordinator) => this.coordinator = coordinator;
    public string Prefix => "coop.debug.naval_lab";
    public string Name => "drift-status";
    public string Description => "Read retained bounded station-drift aggregates, not native getters or a physics cut.";
    public CoopCommandSide Side => CoopCommandSide.Client;
    public IExpectedArgs[] ExpectedArgs => Array.Empty<IExpectedArgs>();
    public CoopCommandResult ProcessCommand(ICoopCommandArgs args) => new CoopCommandResult(true,
        "LIVE_TEST_JSON=" + JsonConvert.SerializeObject(coordinator.InspectDrift()));
}
#endif
