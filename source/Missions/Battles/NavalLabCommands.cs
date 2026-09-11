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
    public string Description => "Start the isolated two-client lab: activation (default), held-helm, factory-authority-probe or two-client-native (experimental follower contact).";
    public CoopCommandSide Side => CoopCommandSide.Server;
    public IExpectedArgs[] ExpectedArgs { get; } =
    {
        new ExpectedArgs("operation_id", "Idempotent operation UUID.", true),
        new ExpectedArgs("first_controller", "First connected client controller id.", true),
        new ExpectedArgs("second_controller", "Second distinct connected client controller id.", true),
        new ExpectedArgs("mode", "activation (default), held-helm, factory-authority-probe or two-client-native; one incarnation per run.", false)
    };
    private static NavalLabMode ParseMode(ICoopCommandArgs args)
    {
        if (args.Count == 3 || args[3] == "activation") return NavalLabMode.Activation;
        if (args[3] == "held-helm") return NavalLabMode.HeldHelm;
        if (args[3] == "two-client-native") return NavalLabMode.TwoClientNative;
        if (args[3] == "factory-authority-probe") return NavalLabMode.FactoryAuthorityProbe;
        throw new ArgumentException("Mode must be activation, held-helm factory-authority-probe or two-client-native.");
    }
    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        try { return new CoopCommandResult(true, "LIVE_TEST_JSON=" + JsonConvert.SerializeObject(coordinator.Create(Guid.Parse(args[0]), args[1], args[2], ParseMode(args)))); }
        catch (Exception exception) { return new CoopCommandResult(false, exception.Message, "naval_lab_rejected"); }
    }
}

public sealed class NavalLabCreateSingleCommand : ICoopCommand
{
    private readonly INavalLabCoordinator coordinator;
    public NavalLabCreateSingleCommand(INavalLabCoordinator coordinator) => this.coordinator = coordinator;
    public string Prefix => "coop.debug.naval_lab";
    public string Name => "create-single";
    public string Description => "One connected client, one native ship and four AI crew; preassigned synthetic captain, not a campaign battle.";
    public CoopCommandSide Side => CoopCommandSide.Server;
    public IExpectedArgs[] ExpectedArgs { get; } =
    {
        new ExpectedArgs("operation_id", "Idempotent operation UUID.", true),
        new ExpectedArgs("controller", "Connected client controller id.", true)
    };
    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        try { return new CoopCommandResult(true, "LIVE_TEST_JSON=" + JsonConvert.SerializeObject(coordinator.CreateSingle(Guid.Parse(args[0]), args[1]))); }
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
        new ExpectedArgs("kind", "helm (1s), probe (30s helm + samples), walk/turn/crew (1s), jump (edge), take-helm (30s held use), release-helm, complete-deployment (native UI modes), sail-full/sail-raised/sail-square-raised, native-axes-pulse (<=1s, rudder=lateral, row=forward), native-axes-backward (<=1s, rudder=lateral, row=false), native-axes-neutral/native-row-stop (<=1s, rudder=0,row=false; preserve sail), native-take-helm/native-release-helm (two-client synthetic tests, not keyboard evidence; await helm-status observation), stop.", true),
        new ExpectedArgs("ship", "Manifest ship index, 0 or 1.", true),
        new ExpectedArgs("rudder", "Finite [-1,1]: helm/probe rudder, walk forward input, turn radians/sec; jump/crew/take-helm/release-helm require 0.", true),
        new ExpectedArgs("row", "Oars for helm/probe; agent actions require false.", true)
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

public sealed class NavalLabSamplesCommand : ICoopCommand
{
    private readonly INavalLabCoordinator coordinator;
    public NavalLabSamplesCommand(INavalLabCoordinator coordinator) => this.coordinator = coordinator;
    public string Prefix => "coop.debug.naval_lab";
    public string Name => "samples";
    public string Description => "Read up to eight retained source-paired observations after a sequence (0 starts a page). Not a physics cut.";
    public CoopCommandSide Side => CoopCommandSide.Both;
    public IExpectedArgs[] ExpectedArgs { get; } = { new ExpectedArgs("after_sequence", "Nonnegative source sequence cursor.", true) };
    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        if (!long.TryParse(args[0], NumberStyles.None, CultureInfo.InvariantCulture, out var cursor))
            return new CoopCommandResult(false, "Invalid sequence cursor.", "invalid_sequence");
        return new CoopCommandResult(true, "LIVE_TEST_JSON=" + JsonConvert.SerializeObject(coordinator.Samples(cursor)));
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
