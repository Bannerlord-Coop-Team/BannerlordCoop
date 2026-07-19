using Common;
using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using TaleWorlds.Library;

namespace GameInterface.Services.LiveTesting;

public interface ILiveTestCommandDispatcher
{
    LiveTestCommandResult Execute(string command, List<string> arguments);
}

public class LiveTestCommandDispatcher : ILiveTestCommandDispatcher
{
    private const string AllowedCommandPrefix = "coop.debug.";

    private static bool functionsCollected;

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
                if (functionsCollected == false)
                {
                    CommandLineFunctionality.CollectCommandLineFunctions();
                    functionsCollected = true;
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
