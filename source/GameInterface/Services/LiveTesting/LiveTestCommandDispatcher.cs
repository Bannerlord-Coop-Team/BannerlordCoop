using Common;
using Common.Commands;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using TaleWorlds.Library;

namespace GameInterface.Services.LiveTesting;

public interface ILiveTestCommandDispatcher
{
    bool EnsureReady();

    IReadOnlyList<string> GetCommandNames();

    LiveTestCommandResult Execute(string command, List<string> arguments);
}

public class LiveTestCommandDispatcher : ILiveTestCommandDispatcher
{
    private const string LegacyDebugCommandPrefix = "coop.debug.";

    private static bool functionsCollected;

    private readonly ICoopCommandRegistry commandRegistry;
    private readonly ICoopCommandArgsFactory argsFactory;

    public LiveTestCommandDispatcher()
    {
    }

    public LiveTestCommandDispatcher(
        ICoopCommandRegistry commandRegistry,
        ICoopCommandArgsFactory argsFactory)
    {
        if (commandRegistry == null) throw new ArgumentNullException(nameof(commandRegistry));
        if (argsFactory == null) throw new ArgumentNullException(nameof(argsFactory));

        this.commandRegistry = commandRegistry;
        this.argsFactory = argsFactory;
    }

    public bool EnsureReady()
    {
        ExceptionDispatchInfo exception = null;

        GameThread.Run(() =>
        {
            try
            {
                EnsureFunctionsCollected();
            }
            catch (Exception e)
            {
                exception = ExceptionDispatchInfo.Capture(e);
            }
        }, blocking: true);

        exception?.Throw();
        return true;
    }

    public IReadOnlyList<string> GetCommandNames()
    {
        IReadOnlyList<string> commandNames = null;
        ExceptionDispatchInfo exception = null;

        GameThread.Run(() =>
        {
            try
            {
                EnsureFunctionsCollected();
                FieldInfo allFunctionsField = typeof(CommandLineFunctionality).GetField(
                    "AllFunctions",
                    BindingFlags.NonPublic | BindingFlags.Static);
                if (allFunctionsField == null ||
                    !(allFunctionsField.GetValue(null) is IDictionary allFunctions))
                {
                    throw new InvalidOperationException("Unable to read the Bannerlord command registry");
                }

                IEnumerable<string> registeredCommands = commandRegistry == null
                    ? Enumerable.Empty<string>()
                    : commandRegistry.Commands.Select(command => command.FullName);
                commandNames = allFunctions.Keys
                    .Cast<string>()
                    .Where(command => command.StartsWith(LegacyDebugCommandPrefix, StringComparison.Ordinal))
                    .Concat(registeredCommands)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(command => command, StringComparer.Ordinal)
                    .ToArray();
            }
            catch (Exception e)
            {
                exception = ExceptionDispatchInfo.Capture(e);
            }
        }, blocking: true);

        exception?.Throw();
        return commandNames;
    }

    public LiveTestCommandResult Execute(string command, List<string> arguments)
    {
        if (string.IsNullOrEmpty(command) ||
            (!(commandRegistry?.Contains(command) ?? false) &&
             !command.StartsWith(LegacyDebugCommandPrefix, StringComparison.Ordinal)))
        {
            return new LiveTestCommandResult(false, "Only registered co-op commands and legacy coop.debug.* commands may be run through live testing");
        }

        if (arguments == null) throw new ArgumentNullException(nameof(arguments));

        LiveTestCommandResult result = null;
        ExceptionDispatchInfo exception = null;

        GameThread.Run(() =>
        {
            try
            {
                EnsureFunctionsCollected();

                if (commandRegistry != null && commandRegistry.Contains(command))
                {
                    ICoopCommandArgs commandArgs = argsFactory.FromValues(arguments);
                    CoopCommandResult commandResult = commandRegistry.ProcessCommand(command, commandArgs);
                    result = new LiveTestCommandResult(true, commandResult.Output);
                    return;
                }

                string output = CommandLineFunctionality.CallFunction(command, arguments, out bool found);
                result = new LiveTestCommandResult(found, output);
            }
            catch (Exception e)
            {
                exception = ExceptionDispatchInfo.Capture(e);
            }
        }, blocking: true);

        exception?.Throw();
        return result;
    }

    private static void EnsureFunctionsCollected()
    {
        if (functionsCollected) return;

        CommandLineFunctionality.CollectCommandLineFunctions();
        functionsCollected = true;
    }
}

public class LiveTestCommandResult
{
    public LiveTestCommandResult(bool found, string output)
    {
        Found = found;
        Output = output;
    }

    public bool Found { get; }

    public string Output { get; }
}
