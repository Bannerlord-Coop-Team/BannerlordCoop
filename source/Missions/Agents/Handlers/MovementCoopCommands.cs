using Common.Commands;
using System;
using System.Collections.Generic;

namespace Missions.Agents.Handlers;

public interface IMovementLegacyCommandResult
{
    CoopCommandResult FromOutput(string output);
}

public sealed class MovementLegacyCommandResult : IMovementLegacyCommandResult
{
    public CoopCommandResult FromOutput(string output)
    {
        if (output == null) return new CoopCommandResult(false, "Command returned no output.", "command_failed");

        bool succeeded = !LooksLikeFailure(output);
        return new CoopCommandResult(succeeded, output, succeeded ? null : "command_failed");
    }

    private static bool LooksLikeFailure(string output)
    {
        string[] failurePrefixes =
        {
            "Usage:",
            "Failed",
            "Unable",
            "No ",
            "Run this",
            "Command can",
            "The host has disabled",
            "Cannot ",
            "Could not",
            "Refusing",
            "Prepare ",
            "Both ",
            "Player parties must",
            "Attacker and defender",
            "Exists must",
            "State requires",
            "A ",
            "The ",
            "Party ",
            "Character ",
            "Settlement ",
            "Object manager",
            "Network ",
            "Battle agent",
            "Active mount",
            "Upgrade ",
            "Clan-party",
            "Map event",
            "Invalid ",
            "Player parties",
            "Player party",
            "Player '",
        };

        foreach (string prefix in failurePrefixes)
        {
            if (output.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return true;
        }

        return false;
    }
}

#if DEBUG
public interface IStateCommand : ICoopCommand
{
}

public sealed class StateCommand : IStateCommand
{
    private readonly IMovementLegacyCommandResult resultFactory;

    public StateCommand(IMovementLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.movement";

    public string Name => "state";

    public string Description => "Reports state.";

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = Missions.Agents.Handlers.MovementDebugCommands.State(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}
#endif

#if DEBUG
public interface IForceRateCommand : ICoopCommand
{
}

public sealed class ForceRateCommand : IForceRateCommand
{
    private readonly IMovementLegacyCommandResult resultFactory;

    public ForceRateCommand(IMovementLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.movement";

    public string Name => "force_rate";

    public string Description => "Runs the force rate debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("rate", "The rate.", true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = Missions.Agents.Handlers.MovementDebugCommands.ForceRate(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}
#endif

#if DEBUG
public interface IForceReceiverCapCommand : ICoopCommand
{
}

public sealed class ForceReceiverCapCommand : IForceReceiverCapCommand
{
    private readonly IMovementLegacyCommandResult resultFactory;

    public ForceReceiverCapCommand(IMovementLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.movement";

    public string Name => "force_receiver_cap";

    public string Description => "Runs the force receiver cap debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("receiver_cap", "The receiver cap.", true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = Missions.Agents.Handlers.MovementDebugCommands.ForceReceiverCap(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}
#endif

#if DEBUG
public interface ISimulateReceivePressureCommand : ICoopCommand
{
}

public sealed class SimulateReceivePressureCommand : ISimulateReceivePressureCommand
{
    private readonly IMovementLegacyCommandResult resultFactory;

    public SimulateReceivePressureCommand(IMovementLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.movement";

    public string Name => "simulate_receive_pressure";

    public string Description => "Runs the simulate receive pressure debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("duration_seconds", "The duration seconds.", true),
        new ExpectedArgs("queue_ms", "The queue ms.", true),
        new ExpectedArgs("apply_ms", "The apply ms.", true),
        new ExpectedArgs("snapshots", "The snapshots.", true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = Missions.Agents.Handlers.MovementDebugCommands.SimulateReceivePressure(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}
#endif

#if DEBUG
public interface IClearReceivePressureCommand : ICoopCommand
{
}

public sealed class ClearReceivePressureCommand : IClearReceivePressureCommand
{
    private readonly IMovementLegacyCommandResult resultFactory;

    public ClearReceivePressureCommand(IMovementLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.movement";

    public string Name => "clear_receive_pressure";

    public string Description => "Restores or clears clear receive pressure.";

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = Missions.Agents.Handlers.MovementDebugCommands.ClearReceivePressure(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}
#endif
