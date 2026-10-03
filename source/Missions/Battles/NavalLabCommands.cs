#if DEBUG
using Common.Commands;
using Newtonsoft.Json;
using System;
using System.Globalization;

namespace Missions.Battles;

public sealed class NavalLabCreateCommand : ICoopCommand
{
    private readonly INavalLabCoordinator coordinator;
    public NavalLabCreateCommand(INavalLabCoordinator coordinator) => this.coordinator = coordinator;
    public string Prefix => "coop.debug.naval_lab";
    public string Name => "create";
    public string Description => "Start the isolated two-client lab: two-client-native; two-client-native-all-physics deliberately combines native physics and network corrections on foreign hulls.";
    public CoopCommandSide Side => CoopCommandSide.Server;
    public IExpectedArgs[] ExpectedArgs { get; } =
    {
        new ExpectedArgs("operation_id", "Idempotent operation UUID.", true),
        new ExpectedArgs("first_controller", "First connected client controller id.", true),
        new ExpectedArgs("second_controller", "Second distinct connected client controller id.", true),
        new ExpectedArgs("mode", "two-client-native or two-client-native-all-physics (disposable diagnostic); one incarnation per run.", true),
        new ExpectedArgs("hulls_per_participant", "Optional 1 (default) or 2; slots 0-1 are crewed flagships, later slots are AI-captained hulls of participant slot % 2.", false)
    };
    private static NavalLabMode ParseMode(ICoopCommandArgs args)
    {
        string mode = args.Count > 3 ? args[3] : null;
        if (mode == "two-client-native") return NavalLabMode.TwoClientNative;
        if (mode == "two-client-native-all-physics") return NavalLabMode.TwoClientNativeAllPhysics;
        throw new ArgumentException("Mode must be two-client-native or two-client-native-all-physics.");
    }
    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        try
        {
            int hulls = args.Count > 4 ? int.Parse(args[4], CultureInfo.InvariantCulture) : 1;
            return new CoopCommandResult(true, "LIVE_TEST_JSON=" + JsonConvert.SerializeObject(coordinator.Create(Guid.Parse(args[0]), args[1], args[2], ParseMode(args), hulls)));
        }
        catch (Exception exception) { return new CoopCommandResult(false, exception.Message, "naval_lab_rejected"); }
    }
}

public sealed class NavalLabActionCommand : ICoopCommand
{
    private readonly INavalLabCoordinator coordinator;
    public NavalLabActionCommand(INavalLabCoordinator coordinator) => this.coordinator = coordinator;
    public string Prefix => "coop.debug.naval_lab";
    public string Name => "action";
    public string Description => "Route controls to the original owner; sail test actions use the same permitted native input relay, not keyboard evidence.";
    public CoopCommandSide Side => CoopCommandSide.Server;
    public IExpectedArgs[] ExpectedArgs { get; } =
    {
        new ExpectedArgs("operation_id", "Idempotent operation UUID.", true),
        new ExpectedArgs("kind", "walk/turn (1s deck locomotion, two-client-native only), complete-deployment, sail-full/sail-raised/sail-square-raised, native-axes-pulse (<=1s, rudder=lateral, row=forward), native-axes-backward (<=1s, rudder=lateral, row=false), native-axes-neutral/native-row-stop (<=1s, rudder=0,row=false; preserve sail), native-take-helm/native-release-helm (two-client synthetic tests, not keyboard evidence; await helm-status observation), fleet-follow/fleet-stop (secondary hull slot, rudder=0, row=false; vanilla ShipOrder on the owner), stop.", true),
        new ExpectedArgs("ship", "Manifest ship index: 0 or 1 for flagships, 2 or 3 for secondary hulls when created with 2 hulls per participant.", true),
        new ExpectedArgs("rudder", "Finite [-1,1]: axes lateral, walk forward input, turn radians/sec.", true),
        new ExpectedArgs("row", "Forward rowing for native-axes-pulse; other actions require false.", true)
    };
    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        try
        {
            return new CoopCommandResult(true, "LIVE_TEST_JSON=" + JsonConvert.SerializeObject(coordinator.Execute(
                Guid.Parse(args[0]), args[1], int.Parse(args[2], CultureInfo.InvariantCulture),
                float.Parse(args[3], CultureInfo.InvariantCulture), bool.Parse(args[4]))));
        }
        catch (Exception exception) { return new CoopCommandResult(false, exception.Message, "naval_lab_rejected"); }
    }
}

public sealed class NavalLabInspectCommand : ICoopCommand
{
    private readonly INavalLabCoordinator coordinator;
    public NavalLabInspectCommand(INavalLabCoordinator coordinator) => this.coordinator = coordinator;
    public string Prefix => "coop.debug.naval_lab";
    public string Name => "inspect";
    public string Description => "Read fixture identity, elected authority and native force/contact observations.";
    public CoopCommandSide Side => CoopCommandSide.Both;
    public IExpectedArgs[] ExpectedArgs => Array.Empty<IExpectedArgs>();
    public CoopCommandResult ProcessCommand(ICoopCommandArgs args) => new CoopCommandResult(true,
        "LIVE_TEST_JSON=" + JsonConvert.SerializeObject(coordinator.Inspect()));
}

public sealed class NavalLabSailStatusCommand : ICoopCommand
{
    private readonly INavalLabCoordinator coordinator;
    public NavalLabSailStatusCommand(INavalLabCoordinator coordinator) => this.coordinator = coordinator;
    public string Prefix => "coop.debug.naval_lab";
    public string Name => "sail-status";
    public string Description => "Read bounded sail request, host observation and owner HUD freshness. Never changes input or physics.";
    public CoopCommandSide Side => CoopCommandSide.Both;
    public IExpectedArgs[] ExpectedArgs => Array.Empty<IExpectedArgs>();
    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        if (args.Count != 0) return new CoopCommandResult(false, "No arguments expected.", "invalid_arguments");
        return new CoopCommandResult(true, "LIVE_TEST_JSON=" + JsonConvert.SerializeObject(coordinator.SailStatus()));
    }
}

public sealed class NavalLabHelmStatusCommand : ICoopCommand
{
    private readonly INavalLabCoordinator coordinator;
    public NavalLabHelmStatusCommand(INavalLabCoordinator coordinator) => this.coordinator = coordinator;
    public string Prefix => "coop.debug.naval_lab";
    public string Name => "helm-status";
    public string Description => "Read owner-local synthetic helm dispatch and subsequent-tick occupancy observation, not keyboard or remote replication evidence.";
    public CoopCommandSide Side => CoopCommandSide.Both;
    public IExpectedArgs[] ExpectedArgs => Array.Empty<IExpectedArgs>();
    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        if (args.Count != 0) return new CoopCommandResult(false, "No arguments expected.", "invalid_arguments");
        return new CoopCommandResult(true, "LIVE_TEST_JSON=" + JsonConvert.SerializeObject(coordinator.HelmStatus()));
    }
}

public sealed class NavalLabControlStatusCommand : ICoopCommand
{
    private readonly INavalLabCoordinator coordinator;
    public NavalLabControlStatusCommand(INavalLabCoordinator coordinator) => this.coordinator = coordinator;
    public string Prefix => "coop.debug.naval_lab";
    public string Name => "control-status";
    public string Description => "Read input eligibility, host captured/follower accepted/displayed/native-observed sail/oar presentation for both slots, and safety counters. Not rendered or propulsion proof.";
    public CoopCommandSide Side => CoopCommandSide.Both;
    public IExpectedArgs[] ExpectedArgs => Array.Empty<IExpectedArgs>();
    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        if (args.Count != 0) return new CoopCommandResult(false, "No arguments expected.", "invalid_arguments");
        return new CoopCommandResult(true, "LIVE_TEST_JSON=" + JsonConvert.SerializeObject(coordinator.ControlStatus()));
    }
}

public sealed class NavalLabReceiptCommand : ICoopCommand
{
    private readonly INavalLabSessionStore store;
    public NavalLabReceiptCommand(INavalLabSessionStore store) => this.store = store;
    public string Prefix => "coop.debug.naval_lab";
    public string Name => "receipt";
    public string Description => "Read an operation receipt after outcomeUncertain; do not blindly retry.";
    public CoopCommandSide Side => CoopCommandSide.Server;
    public IExpectedArgs[] ExpectedArgs { get; } = { new ExpectedArgs("operation_id", "Operation UUID.", true) };
    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        if (!Guid.TryParse(args[0], out var id)) return new CoopCommandResult(false, "Invalid operation id.", "invalid_id");
        return new CoopCommandResult(true, "LIVE_TEST_JSON=" + JsonConvert.SerializeObject(store.InspectOperation(id)));
    }
}
#endif
