using Autofac;
using Common;
using GameInterface.Services.ObjectManager;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.ObjectSystem;

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

    public static bool TryGetManagedObject<T>(
        IObjectManager objectManager,
        string id,
        out T value,
        out string error)
        where T : class
    {
        value = null;
        error = null;

        if (objectManager == null)
        {
            error = "ObjectManager is null.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(id))
        {
            error = $"Object id for {typeof(T).Name} cannot be empty.";
            return false;
        }

        if (objectManager.TryGetObject<T>(id, out value))
            return true;

        error = $"No {typeof(T).Name} found with id '{id}'.";
        return false;
    }

    public static bool TryGetMbObject<T>(
        string stringId,
        out T value,
        out string error)
        where T : MBObjectBase
    {
        value = null;
        error = null;

        if (string.IsNullOrWhiteSpace(stringId))
        {
            error = $"String id for {typeof(T).Name} cannot be empty.";
            return false;
        }

        value = MBObjectManager.Instance.GetObject<T>(stringId);

        if (value != null)
            return true;

        error = $"No {typeof(T).Name} found with string id '{stringId}'.";
        return false;
    }

    public static bool TryGetMobileParty(
        string stringId,
        out MobileParty mobileParty,
        out string error)
    {
        mobileParty = null;

        if (!TryGetMbObject(stringId, out mobileParty, out error))
            return false;

        if (mobileParty.Party != null)
            return true;

        error = $"MobileParty '{mobileParty.StringId}' has no Party.";
        return false;
    }

    public static string FormatException(string action, Exception ex)
    {
        return
            $"{action} failed.\n" +
            $"Exception: {ex.GetType().Name}\n" +
            $"Message: {ex.Message}";
    }
}