using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Diamond;

namespace GameInterface.Registry.Auto;
internal class LifetimePatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<LifetimePatches>();

    internal static bool CreatePrefix(object __instance, MethodBase __originalMethod)
    {
        // Call original if we call this function
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client created unmanaged {name}\n"
                + "Callstack: {callstack}", typeof(BesiegerCamp), Environment.StackTrace);
            return true;
        }

        var message = CreateInstanceCreatedEventFast(__instance, __originalMethod.DeclaringType);

        MessageBroker.Instance.Publish(__instance, message);

        return true;
    }

    internal static bool DestroyPrefix(ref object __instance, MethodBase __originalMethod)
    {
        // Call original if we call this function
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client destroyed unmanaged {name}\n"
                + "Callstack: {callstack}", typeof(BesiegerCamp), Environment.StackTrace);
            return true;
        }

        var destroyMessage = CreateInstanceDestroyedEventFast(__instance, __originalMethod.DeclaringType);

        MessageBroker.Instance.Publish(__instance, destroyMessage);

        return true;
    }

    private static Dictionary<Type, IEvent> CreateEventsCache = new Dictionary<Type, IEvent>();
    public static IEvent CreateInstanceCreatedEventFast(object obj, Type type)
    {
        IEvent @event;
        // Optimization for multithreading
        if (CreateEventsCache.TryGetValue(type, out @event)) return @event;

        lock (CreateEventsCache)
        {
            if (CreateEventsCache.TryGetValue(type, out @event)) return @event;

            Type genericTypeDefinition = typeof(InstanceCreated<>).MakeGenericType(type);

            var createEvent = (IEvent)Activator.CreateInstance(genericTypeDefinition, obj);

            CreateEventsCache.Add(type, createEvent);

            return createEvent;
        }
    }

    private static Dictionary<Type, IEvent> DestroyEventsCache = new Dictionary<Type, IEvent>();
    public static IEvent CreateInstanceDestroyedEventFast(object obj, Type type)
    {
        IEvent @event;
        // Optimization for multithreading
        if (DestroyEventsCache.TryGetValue(type, out @event)) return @event;

        lock (DestroyEventsCache)
        {
            if (DestroyEventsCache.TryGetValue(type, out @event)) return @event;

            Type genericTypeDefinition = typeof(InstanceCreated<>).MakeGenericType(type);

            var destroyEvent = (IEvent)Activator.CreateInstance(genericTypeDefinition, obj);

            DestroyEventsCache.Add(type, destroyEvent);

            return destroyEvent;
        }
    }

    private static IEnumerable<Type> GetAllBaseTypes(Type obj)
    {
        yield return obj;

        foreach (var inheritedType in obj.GetInterfaces())
        {
            yield return inheritedType;
        }

        if (obj.BaseType != null &&
            obj.BaseType != typeof(object))
        {
            foreach (var baseType in GetAllBaseTypes(obj.BaseType))
            {
                yield return baseType;
            }
        }
    }
}