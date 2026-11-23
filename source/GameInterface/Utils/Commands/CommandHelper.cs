using Autofac;
using Common;
using GameInterface.Services.ObjectManager;
using System;
using System.Collections.Generic;

namespace GameInterface.Utils.Commands;

internal static class CommandHelpers
{
    public static bool IsServerOnlyCommand(out string error, string commandName)
    {
        error = null;

        if (!ModInformation.IsClient)
            return true;

        error = $"The '{commandName}' command cannot be used on the client. It is intended for server use only.";
        return false;
    }

    public static bool HasArgCount(
        List<string> args,
        int expectedCount,
        string usage,
        out string error)
    {
        error = null;

        if (args != null && args.Count == expectedCount)
            return true;

        error = usage;
        return false;
    }

    public static bool TryGetRequiredArg(
        List<string> args,
        int index,
        string argName,
        string usage,
        out string value,
        out string error)
    {
        value = null;
        error = null;

        if (args == null || args.Count <= index)
        {
            error = usage;
            return false;
        }

        value = args[index];

        if (!string.IsNullOrWhiteSpace(value))
            return true;

        error = $"Missing required argument: {argName}.\n\n{usage}";
        return false;
    }

    public static bool TryGetObjectManager(out IObjectManager objectManager, out string error)
    {
        objectManager = null;
        error = null;

        if (!ContainerProvider.TryGetContainer(out var container))
        {
            error = "Could not resolve Autofac container.";
            return false;
        }

        if (!container.TryResolve(out objectManager))
        {
            error = "Could not resolve ObjectManager from container.";
            return false;
        }

        return true;
    }

    public static string FormatException(string action, Exception ex)
    {
        return
            $"{action} failed.\n" +
            $"Exception: {ex.GetType().Name}\n" +
            $"Message: {ex.Message}";
    }
}
