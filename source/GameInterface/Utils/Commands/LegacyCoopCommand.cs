using Common.Commands;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GameInterface.Utils.Commands;

public interface ILegacyCoopCommandExecutor
{
    CoopCommandResult Execute(ICoopCommandArgs args, Func<List<string>, string> command);
}

public sealed class LegacyCoopCommandExecutor : ILegacyCoopCommandExecutor
{
    public CoopCommandResult Execute(ICoopCommandArgs args, Func<List<string>, string> command)
    {
        if (args == null) throw new ArgumentNullException(nameof(args));
        if (command == null) throw new ArgumentNullException(nameof(command));

        string output = command(args.ToList()) ?? string.Empty;
        if (IsFailure(output))
            return new CoopCommandResult(false, output, "command_rejected");

        return new CoopCommandResult(true, output);
    }

    private static bool IsFailure(string output)
    {
        return output.StartsWith("Usage:", StringComparison.OrdinalIgnoreCase) ||
               output.StartsWith("Unable", StringComparison.OrdinalIgnoreCase) ||
               output.StartsWith("Command ", StringComparison.OrdinalIgnoreCase) ||
               output.StartsWith("Run ", StringComparison.OrdinalIgnoreCase) ||
               output.StartsWith("No ", StringComparison.OrdinalIgnoreCase) ||
               output.StartsWith("A ", StringComparison.OrdinalIgnoreCase) ||
               output.StartsWith("The ", StringComparison.OrdinalIgnoreCase) ||
               output.StartsWith("Enter ", StringComparison.OrdinalIgnoreCase) ||
               output.StartsWith("Finish ", StringComparison.OrdinalIgnoreCase) ||
               output.StartsWith("Could not", StringComparison.OrdinalIgnoreCase) ||
               output.StartsWith("Couldnt", StringComparison.OrdinalIgnoreCase) ||
               output.StartsWith("Failed", StringComparison.OrdinalIgnoreCase) ||
               output.StartsWith("Unknown", StringComparison.OrdinalIgnoreCase) ||
               output.StartsWith("Unsupported", StringComparison.OrdinalIgnoreCase) ||
               output.IndexOf(" not found", StringComparison.OrdinalIgnoreCase) >= 0 ||
               output.IndexOf(" cannot be ", StringComparison.OrdinalIgnoreCase) >= 0 ||
               output.IndexOf(" does not ", StringComparison.OrdinalIgnoreCase) >= 0 ||
               output.IndexOf(" did not ", StringComparison.OrdinalIgnoreCase) >= 0 ||
               output.IndexOf(" has no ", StringComparison.OrdinalIgnoreCase) >= 0 ||
               output.IndexOf(" is only ", StringComparison.OrdinalIgnoreCase) >= 0 ||
               output.IndexOf(" is unavailable", StringComparison.OrdinalIgnoreCase) >= 0 ||
               output.IndexOf(" is not ", StringComparison.OrdinalIgnoreCase) >= 0 ||
               output.IndexOf(" is out of ", StringComparison.OrdinalIgnoreCase) >= 0 ||
               output.IndexOf(" has not arrived", StringComparison.OrdinalIgnoreCase) >= 0 ||
               output.IndexOf(" failed", StringComparison.OrdinalIgnoreCase) >= 0 ||
               output.IndexOf("rejected", StringComparison.OrdinalIgnoreCase) >= 0;
    }
}

public abstract class LegacyCoopCommand : ICoopCommand
{
    private readonly ILegacyCoopCommandExecutor executor;
    private readonly Func<List<string>, string> command;

    protected LegacyCoopCommand(
        ILegacyCoopCommandExecutor executor,
        string prefix,
        string name,
        string description,
        IExpectedArgs[] expectedArgs,
        Func<List<string>, string> command)
    {
        if (executor == null) throw new ArgumentNullException(nameof(executor));
        if (command == null) throw new ArgumentNullException(nameof(command));

        this.executor = executor;
        this.command = command;
        Prefix = prefix;
        Name = name;
        Description = description;
        ExpectedArgs = expectedArgs;
    }

    protected LegacyCoopCommand(
        string prefix,
        string name,
        string description,
        IExpectedArgs[] expectedArgs,
        Func<List<string>, string> command)
    {
        if (command == null) throw new ArgumentNullException(nameof(command));

        this.command = command;
        Prefix = prefix;
        Name = name;
        Description = description;
        ExpectedArgs = expectedArgs;
    }

    public string Prefix { get; }

    public string Name { get; }

    public string Description { get; }

    public IExpectedArgs[] ExpectedArgs { get; }

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        if (executor != null) return executor.Execute(args, command);

        var values = new List<string>(args.Count);
        foreach (string value in args)
        {
            values.Add(value);
        }

        string output = command(values);
        return LegacyCommandSucceeded(output)
            ? new CoopCommandResult(true, output)
            : new CoopCommandResult(false, output, "command_rejected");
    }

    internal static bool LegacyCommandSucceeded(string output)
    {
        if (output == null) return false;
        if (output == "No characters" || output == "No special items") return true;

        string[] failurePrefixes =
        {
            "Usage:",
            "Invalid usage",
            "Invalid ",
            "Unable ",
            "Error ",
            "Failed ",
            "No ",
            "Run ",
            "Argument",
            "Expected ",
            "Refusing ",
            "Cannot ",
            "Leave ",
            "Restore the previous ",
            "Only found ",
            "Both parties ",
            "A mission is already ",
            "A player encounter with another ",
            "A prisoner-prompt siege fixture is already ",
            "A Danustica tournament fixture is already ",
            "Follow fixture is already ",
            "The client has no ",
            "The fixture has no ",
            "The fixture-created ",
            "The local player party cannot ",
            "The local player party is not ",
            "The player party must ",
            "The parties must ",
            "Player ",
            "Party ",
            "armyPartyCount ",
            "Command can ",
            "Command is ",
            "The command ",
            "The '",
            "This command ",
            "This function ",
            "Create party ",
            "Destroy all ",
            "spawn_test_parties ",
            "verify_ai_authority ",
        };
        foreach (string prefix in failurePrefixes)
        {
            if (output.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return false;
        }

        return output.IndexOf(" not found", StringComparison.OrdinalIgnoreCase) < 0 &&
               output.IndexOf(" was not found", StringComparison.OrdinalIgnoreCase) < 0 &&
               output.IndexOf(" is not active", StringComparison.OrdinalIgnoreCase) < 0 &&
               output.IndexOf(" is not a ", StringComparison.OrdinalIgnoreCase) < 0 &&
               output.IndexOf(" is not under ", StringComparison.OrdinalIgnoreCase) < 0 &&
               output.IndexOf(" is already ", StringComparison.OrdinalIgnoreCase) < 0 &&
               output.IndexOf(" already has ", StringComparison.OrdinalIgnoreCase) < 0 &&
               output.IndexOf(" does not ", StringComparison.OrdinalIgnoreCase) < 0 &&
               output.IndexOf(" was not a valid ", StringComparison.OrdinalIgnoreCase) < 0 &&
               output.IndexOf(" must be ", StringComparison.OrdinalIgnoreCase) < 0 &&
               output.IndexOf(" can only be ", StringComparison.OrdinalIgnoreCase) < 0;
    }
}
