using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Common.Commands;

public interface ICoopCommandRegistry
{
    IReadOnlyList<CoopCommandDescriptor> Commands { get; }

    bool Contains(string fullName);

    CoopCommandResult ProcessCommand(string fullName, ICoopCommandArgs args);
}

public sealed class CoopCommandRegistry : ICoopCommandRegistry
{
    private readonly IReadOnlyDictionary<string, ICoopCommand> commandsByName;
    private readonly IReadOnlyDictionary<string, CoopCommandDescriptor> descriptorsByName;
    private readonly ILogger logger;

    public CoopCommandRegistry(IEnumerable<ICoopCommand> commands, ILogger logger)
    {
        if (commands == null) throw new ArgumentNullException(nameof(commands));
        if (logger == null) throw new ArgumentNullException(nameof(logger));

        this.logger = logger;

        var commandMap = new Dictionary<string, ICoopCommand>(StringComparer.Ordinal);
        var descriptorMap = new Dictionary<string, CoopCommandDescriptor>(StringComparer.Ordinal);
        var descriptors = new List<CoopCommandDescriptor>();
        foreach (ICoopCommand command in commands)
        {
            if (command == null)
                throw new ArgumentException("The command collection cannot contain null values.", nameof(commands));

            IExpectedArgs[] expectedArgs = command.ExpectedArgs;
            ValidateCommand(command, expectedArgs);

            var descriptor = new CoopCommandDescriptor(
                command.Prefix,
                command.Name,
                command.Description,
                expectedArgs);
            if (commandMap.ContainsKey(descriptor.FullName))
                throw new InvalidOperationException($"The command '{descriptor.FullName}' is registered more than once.");

            commandMap.Add(descriptor.FullName, command);
            descriptorMap.Add(descriptor.FullName, descriptor);
            descriptors.Add(descriptor);
        }

        descriptors.Sort((first, second) =>
            StringComparer.Ordinal.Compare(first.FullName, second.FullName));

        commandsByName = new ReadOnlyDictionary<string, ICoopCommand>(commandMap);
        descriptorsByName = new ReadOnlyDictionary<string, CoopCommandDescriptor>(descriptorMap);
        Commands = descriptors.AsReadOnly();
    }

    public IReadOnlyList<CoopCommandDescriptor> Commands { get; }

    public bool Contains(string fullName)
    {
        return fullName != null && commandsByName.ContainsKey(fullName);
    }

    public CoopCommandResult ProcessCommand(string fullName, ICoopCommandArgs args)
    {
        if (args == null) throw new ArgumentNullException(nameof(args));

        if (fullName == null || !commandsByName.TryGetValue(fullName, out ICoopCommand command))
        {
            return new CoopCommandResult(
                false,
                $"Could not find the command {fullName}",
                "command_not_found");
        }

        CoopCommandDescriptor descriptor = descriptorsByName[fullName];
        if (!ArgumentsAreValid(descriptor.ExpectedArgs, args))
            return new CoopCommandResult(false, descriptor.Usage, "invalid_arguments");

        ICoopCommandArgs trimmedArgs = TrimArguments(args);
        try
        {
            CoopCommandResult result = command.ProcessCommand(trimmedArgs);
            if (result == null)
                throw new InvalidOperationException($"The command '{fullName}' returned no result.");

            return result;
        }
        catch (Exception exception)
        {
            logger.Error(exception, "Command {CommandName} failed", fullName);
            return new CoopCommandResult(
                false,
                $"Command '{fullName}' failed: {exception.Message}",
                "command_failed");
        }
    }

    private void ValidateCommand(ICoopCommand command, IExpectedArgs[] expectedArgs)
    {
        if (string.IsNullOrWhiteSpace(command.Prefix))
            throw new InvalidOperationException($"{command.GetType().Name} has no command prefix.");
        if (!string.Equals(command.Prefix, "coop", StringComparison.Ordinal) &&
            !command.Prefix.StartsWith("coop.", StringComparison.Ordinal))
            throw new InvalidOperationException($"The command prefix '{command.Prefix}' must begin with 'coop'.");
        if (command.Prefix.StartsWith(".", StringComparison.Ordinal) ||
            command.Prefix.EndsWith(".", StringComparison.Ordinal) ||
            command.Prefix.Contains("..") ||
            ContainsWhitespace(command.Prefix))
            throw new InvalidOperationException($"The command prefix '{command.Prefix}' is invalid.");
        if (string.IsNullOrWhiteSpace(command.Name) ||
            command.Name.Contains(".") ||
            ContainsWhitespace(command.Name))
            throw new InvalidOperationException($"The command name '{command.Name}' is invalid.");
        if (string.IsNullOrWhiteSpace(command.Description))
            throw new InvalidOperationException($"The command '{command.Prefix}.{command.Name}' has no description.");

        ValidateExpectedArgs(command, expectedArgs);
    }

    private void ValidateExpectedArgs(ICoopCommand command, IExpectedArgs[] expectedArgs)
    {
        if (expectedArgs == null)
            throw new InvalidOperationException($"The command '{command.Prefix}.{command.Name}' has no expected argument definitions.");

        var argumentNames = new HashSet<string>(StringComparer.Ordinal);
        bool optionalArgumentFound = false;
        foreach (IExpectedArgs expectedArg in expectedArgs)
        {
            if (expectedArg == null)
                throw new InvalidOperationException($"The command '{command.Prefix}.{command.Name}' has a null expected argument definition.");
            if (string.IsNullOrWhiteSpace(expectedArg.Name) || ContainsWhitespace(expectedArg.Name))
                throw new InvalidOperationException($"The expected argument name '{expectedArg.Name}' is invalid.");
            if (string.IsNullOrWhiteSpace(expectedArg.Description))
                throw new InvalidOperationException($"The expected argument '{expectedArg.Name}' has no description.");
            if (!argumentNames.Add(expectedArg.Name))
                throw new InvalidOperationException($"The expected argument '{expectedArg.Name}' is defined more than once.");
            if (expectedArg.IsRequired && optionalArgumentFound)
                throw new InvalidOperationException("Required expected arguments cannot follow optional arguments.");

            optionalArgumentFound |= !expectedArg.IsRequired;
        }
    }

    private bool ArgumentsAreValid(IExpectedArgs[] expectedArgs, ICoopCommandArgs args)
    {
        int requiredArgumentCount = 0;
        foreach (IExpectedArgs expectedArg in expectedArgs)
        {
            if (!expectedArg.IsRequired) break;
            requiredArgumentCount++;
        }

        if (args.Count < requiredArgumentCount || args.Count > expectedArgs.Length) return false;

        foreach (string value in args)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
        }

        return true;
    }

    private ICoopCommandArgs TrimArguments(ICoopCommandArgs args)
    {
        var trimmedValues = new List<string>(args.Count);
        foreach (string value in args)
        {
            trimmedValues.Add(value.Trim());
        }

        return new CoopCommandArgs(trimmedValues.AsReadOnly());
    }

    private bool ContainsWhitespace(string value)
    {
        foreach (char character in value)
        {
            if (char.IsWhiteSpace(character)) return true;
        }

        return false;
    }
}
