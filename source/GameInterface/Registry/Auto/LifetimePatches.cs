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

    internal static void DestroyPostfix(ref T __instance)
    {
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