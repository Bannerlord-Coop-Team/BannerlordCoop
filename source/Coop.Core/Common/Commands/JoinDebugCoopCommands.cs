#if DEBUG
using Common.Commands;
using System;
using System.Collections.Generic;

namespace Coop.Core.Common.Commands;

internal static class JoinDebugLegacyCommandResult
{
    public static CoopCommandResult FromOutput(string output)
    {
        if (output == null) return new CoopCommandResult(false, "Command returned no output.", "command_failed");

        bool failed = output.StartsWith("Usage:", StringComparison.OrdinalIgnoreCase) ||
                      output.StartsWith("Failed", StringComparison.OrdinalIgnoreCase) ||
                      output.StartsWith("No ", StringComparison.OrdinalIgnoreCase) ||
                      output.IndexOf(" must be run ", StringComparison.OrdinalIgnoreCase) >= 0 ||
                      output.IndexOf("client-only", StringComparison.OrdinalIgnoreCase) >= 0;
        return failed
            ? new CoopCommandResult(false, output, "command_rejected")
            : new CoopCommandResult(true, output);
    }
}

public interface IJoinStateCommand : ICoopCommand
{
}

public sealed class JoinStateCommand : IJoinStateCommand
{
    public string Prefix => "coop.debug.connection";

    public string Name => "join_state";

    public string Description => "Reports the current campaign join state.";

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        return JoinDebugLegacyCommandResult.FromOutput(
            JoinDebugCommands.JoinState(new List<string>(args)));
    }
}

public interface IArmInactivePartyDeficitCommand : ICoopCommand
{
}

public sealed class ArmInactivePartyDeficitCommand : IArmInactivePartyDeficitCommand
{
    public string Prefix => "coop.debug.connection";

    public string Name => "arm_inactive_party_deficit";

    public string Description => "Arms the next client join baseline to omit an inactive party.";

    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("party_string_id", "The inactive party StringId."),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        return JoinDebugLegacyCommandResult.FromOutput(
            JoinDebugCommands.ArmInactivePartyDeficit(new List<string>(args)));
    }
}

public interface IStageInactivePartyCommand : ICoopCommand
{
}

public sealed class StageInactivePartyCommand : IStageInactivePartyCommand
{
    public string Prefix => "coop.debug.connection";

    public string Name => "stage_inactive_party";

    public string Description => "Stages an isolated server party as inactive for join testing.";

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        return JoinDebugLegacyCommandResult.FromOutput(
            JoinDebugCommands.StageInactiveParty(new List<string>(args)));
    }
}

public interface IRestoreInactivePartyCommand : ICoopCommand
{
}

public sealed class RestoreInactivePartyCommand : IRestoreInactivePartyCommand
{
    public string Prefix => "coop.debug.connection";

    public string Name => "restore_inactive_party";

    public string Description => "Restores the staged inactive server party.";

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        return JoinDebugLegacyCommandResult.FromOutput(
            JoinDebugCommands.RestoreInactiveParty(new List<string>(args)));
    }
}

public interface IDisconnectCommand : ICoopCommand
{
}

public sealed class DisconnectCommand : IDisconnectCommand
{
    public string Prefix => "coop.debug.connection";

    public string Name => "disconnect";

    public string Description => "Disconnects the active client session.";

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        return JoinDebugLegacyCommandResult.FromOutput(
            JoinDebugCommands.Disconnect(new List<string>(args)));
    }
}
#endif
