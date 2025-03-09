using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using Serilog;
using System;
using TaleWorlds.CampaignSystem.Siege;

namespace GameInterface.Registry.Auto;
internal class LifetimePatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<LifetimePatches>();

    internal static bool CreatePrefix(ref object __instance)
    {
        // Call original if we call this function
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client created unmanaged {name}\n"
                + "Callstack: {callstack}", typeof(BesiegerCamp), Environment.StackTrace);
            return true;
        }

        Type genericType = typeof(InstanceCreated<>).MakeGenericType(__instance.GetType());
        var message = (IEvent)Activator.CreateInstance(genericType, __instance);

        MessageBroker.Instance.Publish(__instance, message);

        return true;
    }

    internal static bool DestroyPrefix(ref object __instance)
    {
        // Call original if we call this function
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client destroyed unmanaged {name}\n"
                + "Callstack: {callstack}", typeof(BesiegerCamp), Environment.StackTrace);
            return true;
        }

        Type genericType = typeof(InstanceDestroyed<>).MakeGenericType(__instance.GetType());
        var message = (IEvent)Activator.CreateInstance(genericType, __instance);

        MessageBroker.Instance.Publish(__instance, message);

        return true;
    }
}