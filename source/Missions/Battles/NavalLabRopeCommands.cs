#if DEBUG
using Common.Commands;
using Newtonsoft.Json;
using System;
using System.Globalization;

namespace Missions.Battles;

public sealed class NavalLabRopeActionCommand : ICoopCommand
{
    private readonly INavalRopeCoordinator coordinator;
    public NavalLabRopeActionCommand(INavalRopeCoordinator coordinator) => this.coordinator = coordinator;
    public string Prefix => "coop.debug.naval_lab";
    public string Name => "rope-action";
    public string Description => "Native diagnostic throw/miss/cut on an original-owner hook station. Not keyboard/crew-use evidence; no boarding bridge.";
    public CoopCommandSide Side => CoopCommandSide.Server;
    public IExpectedArgs[] ExpectedArgs { get; } =
    {
        new ExpectedArgs("operation_id", "Idempotent UUID.", true),
        new ExpectedArgs("kind", "throw (explicit target), miss (untargeted native flight), cut.", true),
        new ExpectedArgs("source_ship", "Original source owner slot 0 or 1; cut routes to that same owner.", true),
        new ExpectedArgs("source_station", "Source hook index from rope-status.", true),
        new ExpectedArgs("target_station", "Other hull's target index from rope-status; -1 for miss/cut.", true)
    };
    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        try
        {
            return new CoopCommandResult(true, "LIVE_TEST_JSON=" + JsonConvert.SerializeObject(coordinator.ExecuteRope(
                Guid.Parse(args[0]), args[1], int.Parse(args[2], CultureInfo.InvariantCulture),
                int.Parse(args[3], CultureInfo.InvariantCulture), int.Parse(args[4], CultureInfo.InvariantCulture))));
        }
        catch (Exception exception) { return new CoopCommandResult(false, exception.Message, "rope_rejected"); }
    }
}

public sealed class NavalLabRopeStatusCommand : ICoopCommand
{
    private readonly INavalRopeCoordinator coordinator;
    public NavalLabRopeStatusCommand(INavalRopeCoordinator coordinator) => this.coordinator = coordinator;
    public string Prefix => "coop.debug.naval_lab";
    public string Name => "rope-status";
    public string Description => "Read stable hook/target indexes, native connection lifecycle and owner-endpoint force counters. Not an atomic physics cut.";
    public CoopCommandSide Side => CoopCommandSide.Client;
    public IExpectedArgs[] ExpectedArgs => Array.Empty<IExpectedArgs>();
    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        try { return new CoopCommandResult(true, "LIVE_TEST_JSON=" + JsonConvert.SerializeObject(coordinator.RopeStatus())); }
        catch (Exception exception) { return new CoopCommandResult(false, exception.Message, "rope_status_unavailable"); }
    }
}
#endif
