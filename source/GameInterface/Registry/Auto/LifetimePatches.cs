using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using HarmonyLib;
using Serilog;
using System;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace GameInterface.Registry.Auto;
internal class LifetimePatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<LifetimePatches>();

    private static readonly MethodInfo Publish = AccessTools.Method(typeof(MessageBroker), nameof(MessageBroker.Publish));

    internal static bool CreatePrefix(ref object __instance)
    {
        // Call original if we call this function
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client created managed {name}", __instance.GetType());
            return true;
        }

        var message = CreateInstanceCreatedEvent(__instance);

        Publish.MakeGenericMethod(
            typeof(InstanceCreated<>).MakeGenericType(__instance.GetType()))
            .Invoke(MessageBroker.Instance, new object[] { __instance, message });

        return true;
    }

    internal static bool DestroyPrefix(ref object __instance)
    {
        // Call original if we call this function
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client destroyed managed {name}", __instance.GetType());
            return true;
        }

        var message = CreateInstanceDestroyedEvent(__instance);

        Publish.MakeGenericMethod(
            typeof(InstanceCreated<>).MakeGenericType(__instance.GetType()))
            .Invoke(MessageBroker.Instance, new object[] { __instance, message });

        return true;
    }

    public static IEvent CreateInstanceCreatedEvent(object obj)
    {
        var type = obj.GetType();
        Type genericTypeDefinition = typeof(InstanceCreated<>).MakeGenericType(type);

        return (IEvent)Activator.CreateInstance(genericTypeDefinition, obj);
    }

    public static IEvent CreateInstanceDestroyedEvent(object obj)
    {
        var type = obj.GetType();
        Type genericTypeDefinition = typeof(InstanceDestroyed<>).MakeGenericType(type);

        return (IEvent)Activator.CreateInstance(genericTypeDefinition, obj);
    }
}