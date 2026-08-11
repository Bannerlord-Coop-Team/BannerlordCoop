using Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using TaleWorlds.Library;

namespace GameInterface.Services.LiveTesting;

public interface ILiveTestCommandDispatcher
{
    bool EnsureReady();

    IReadOnlyList<string> GetCommands();

    LiveTestCommandResult Execute(string command, List<string> arguments);
}

public class LiveTestCommandDispatcher : ILiveTestCommandDispatcher
{
    private const string AllowedCommandPrefix = "coop.debug.";

    private static bool functionsCollected;

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

    public IReadOnlyList<string> GetCommands()
    {
        IReadOnlyList<string> commands = null;
        ExceptionDispatchInfo exception = null;

        GameThread.Run(() =>
        {
            try
            {
                EnsureFunctionsCollected();
                commands = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(GetLoadableTypes)
                    .SelectMany(GetStaticMethods)
                    .SelectMany(GetCommandNames)
                    .Where(command =>
                        command.StartsWith(AllowedCommandPrefix, StringComparison.Ordinal) &&
                        CommandLineFunctionality.HasFunctionForCommand(command))
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
        return commands;
    }

    public LiveTestCommandResult Execute(string command, List<string> arguments)
    {
        if (string.IsNullOrEmpty(command) ||
            command.StartsWith(AllowedCommandPrefix, StringComparison.Ordinal) == false)
        {
            return new LiveTestCommandResult(false, $"Only {AllowedCommandPrefix} commands may be run through live testing");
        }

        if (arguments == null) throw new ArgumentNullException(nameof(arguments));

        LiveTestCommandResult result = null;
        ExceptionDispatchInfo exception = null;

        GameThread.Run(() =>
        {
            try
            {
                EnsureFunctionsCollected();

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

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            return exception.Types.Where(type => type != null);
        }
    }

    private static IEnumerable<MethodInfo> GetStaticMethods(Type type)
    {
        try
        {
            return type.GetMethods(
                BindingFlags.Public |
                BindingFlags.NonPublic |
                BindingFlags.Static);
        }
        catch (Exception)
        {
            return Array.Empty<MethodInfo>();
        }
    }

    private static IEnumerable<string> GetCommandNames(MethodInfo method)
    {
        try
        {
            var commands = new List<string>();
            foreach (CustomAttributeData attribute in method.GetCustomAttributesData())
            {
                if (attribute.AttributeType != typeof(CommandLineFunctionality.CommandLineArgumentFunction) ||
                    attribute.ConstructorArguments.Count < 2)
                    continue;

                string name = attribute.ConstructorArguments[0].Value as string;
                string group = attribute.ConstructorArguments[1].Value as string;
                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(group))
                    continue;

                commands.Add(group + "." + name);
            }
            return commands;
        }
        catch (Exception)
        {
            return Array.Empty<string>();
        }
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
