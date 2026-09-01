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

    public string Prefix { get; }

    public string Name { get; }

    public string Description { get; }

    public IExpectedArgs[] ExpectedArgs { get; }

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        return executor.Execute(args, command);
    }
}
