using Common.Commands;
using System;
using System.Collections.Generic;

namespace GameInterface.Services.PartyVisuals.Commands;

public interface IPartyVisualLegacyCommandResult
{
    CoopCommandResult FromOutput(string output);
}

public sealed class PartyVisualLegacyCommandResult : IPartyVisualLegacyCommandResult
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

public interface IBufferStateCommand : ICoopCommand
{
}

public sealed class BufferStateCommand : IBufferStateCommand
{
    private readonly IPartyVisualLegacyCommandResult resultFactory;

    public BufferStateCommand(IPartyVisualLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.party_visuals";

    public string Name => "buffer_state";

    public string Description => "Reports buffer state.";

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.PartyVisuals.Commands.PartyVisualDebugCommands.BufferState(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}

#if DEBUG
public interface IFixtureStateCommand : ICoopCommand
{
}

public sealed class FixtureStateCommand : IFixtureStateCommand
{
    private readonly IPartyVisualLegacyCommandResult resultFactory;

    public FixtureStateCommand(IPartyVisualLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.party_visuals";

    public string Name => "fixture_state";

    public string Description => "Reports fixture state.";

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.PartyVisuals.Commands.PartyVisualDebugCommands.FixtureState(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}
#endif

#if DEBUG
public interface IStageOverLimitFixtureCommand : ICoopCommand
{
}

public sealed class StageOverLimitFixtureCommand : IStageOverLimitFixtureCommand
{
    private readonly IPartyVisualLegacyCommandResult resultFactory;

    public StageOverLimitFixtureCommand(IPartyVisualLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.party_visuals";

    public string Name => "stage_over_limit_fixture";

    public string Description => "Runs the stage over limit fixture debug operation.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("target_eligible_party_count", "The target eligible party count.", true),
        new ExpectedArgs("settlement_id", "The settlement id.", true),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.PartyVisuals.Commands.PartyVisualDebugCommands.StageOverLimitFixture(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}
#endif

#if DEBUG
public interface IRestoreOverLimitFixtureCommand : ICoopCommand
{
}

public sealed class RestoreOverLimitFixtureCommand : IRestoreOverLimitFixtureCommand
{
    private readonly IPartyVisualLegacyCommandResult resultFactory;

    public RestoreOverLimitFixtureCommand(IPartyVisualLegacyCommandResult resultFactory)
    {
        if (resultFactory == null) throw new ArgumentNullException(nameof(resultFactory));

        this.resultFactory = resultFactory;
    }
    public string Prefix => "coop.debug.party_visuals";

    public string Name => "restore_over_limit_fixture";

    public string Description => "Restores or clears restore over limit fixture.";

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        string output = global::GameInterface.Services.PartyVisuals.Commands.PartyVisualDebugCommands.RestoreOverLimitFixture(new List<string>(args));
        return resultFactory.FromOutput(output);
    }
}
#endif
