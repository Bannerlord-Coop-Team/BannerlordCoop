using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using Serilog;
using System;
using TaleWorlds.Core;

namespace GameInterface.Registry.Auto;
internal class LifetimePatches<T>
{
    private static readonly ILogger Logger = LogManager.GetLogger<LifetimePatches<T>>();

    internal static void CreatePrefix(ref T __instance)
    {
        // Call original if we call this function
        if (CallOriginalPolicy.IsOriginalAllowed()) return;

        if (typeof(T) == typeof(Equipment) && TransientEquipmentSyncScope.IsActive) return;

        if (ModInformation.IsClient)
        {
            LogClientCreated(__instance);
            return;
        }

        MessageBroker.Instance.Publish(__instance, new InstanceCreated<T>(__instance));
    }

    // Trade and inventory screens build hundreds of Equipment copies on a client, and a hot caller can build
    // any other managed type just as often, so an error per instance floods the log and stalls weak machines.
    // The stack trace is there to find which caller creates the managed object, so it is logged once per
    // distinct call site and repeats are summed periodically. The field lives in a generic class, so every
    // managed type has its own limiter and a noisy type cannot use up the sites of another.
    private static readonly CallSiteLogLimiter ClientCallSites = new(256, TimeSpan.FromSeconds(10));

    private static void LogClientCreated(T instance)
    {
        var site = CallSiteLogLimiter.GetCallSite(2, 6);
        if (ClientCallSites.ShouldLogFull(site, out int suppressed))
        {
            Logger.Error("Client created managed {name}; caller={Caller}", instance.GetType(), Environment.StackTrace);
        }
        else if (suppressed > 0)
        {
            Logger.Warning("Client created managed {name} {Count} more times from callers already logged", instance.GetType(), suppressed);
        }
    }

    internal static void DestroyPostfix(ref T __instance, bool __runOriginal)
    {
        // A prefix that blocked the destroy method means the object was not actually destroyed, so there
        // is nothing to replicate. A coop prefix blocks a client's local finalize and routes it through a
        // server request instead; without this guard that intercepted call would be logged as a spurious
        // client destroy.
        if (!__runOriginal) return;

        // Call original if we call this function
        if (CallOriginalPolicy.IsOriginalAllowed()) return;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client destroyed managed {name}", __instance.GetType());
            return;
        }

        MessageBroker.Instance.Publish(__instance, new InstanceDestroyed<T>(__instance));
    }
}
