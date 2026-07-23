using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using Serilog;

namespace GameInterface.Registry.Auto;
internal class LifetimePatches<T>
{
    private static readonly ILogger Logger = LogManager.GetLogger<LifetimePatches<T>>();

    internal static void CreatePrefix(ref T __instance)
    {
        // Call original if we call this function
        if (CallOriginalPolicy.IsOriginalAllowed()) return;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client created managed {name}", __instance.GetType());
            return;
        }

        MessageBroker.Instance.Publish(__instance, new InstanceCreated<T>(__instance));
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