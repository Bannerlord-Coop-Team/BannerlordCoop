using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using HarmonyLib;
using Serilog;
using System.Reflection;

namespace GameInterface.Registry.Auto;
internal class LifetimePatches<T>
{
    private static readonly ILogger Logger = LogManager.GetLogger<LifetimePatches<T>>();

    private static readonly MethodInfo Publish = AccessTools.Method(typeof(MessageBroker), nameof(MessageBroker.Publish));

    internal static bool CreatePrefix(ref T __instance)
    {
        // Call original if we call this function
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client created managed {name}", __instance.GetType());
            return true;
        }

        MessageBroker.Instance.Publish(__instance, new InstanceCreated<T>(__instance));

        return true;
    }

    internal static bool DestroyPrefix(ref T __instance)
    {
        // Call original if we call this function
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client destroyed managed {name}", typeof(T));
            return false;
        }

        MessageBroker.Instance.Publish(__instance, new InstanceDestroyed<T>(__instance));

        return true;
    }
}