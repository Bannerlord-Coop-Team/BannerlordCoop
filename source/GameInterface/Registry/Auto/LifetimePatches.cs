using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Reflection;
using TaleWorlds.CampaignSystem.Siege;

namespace GameInterface.Registry.Auto;
internal class LifetimePatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<LifetimePatches>();

    internal static void CreatePrefix(object __instance, MethodBase __originalMethod)
    {
        // Call original if we call this function
        if (CallPolicy.IsOriginalAllowed()) return;

        if (ContainerProvider.TryResolve<IGameInterfaceConfig>(out var config) == false) return;

        if (config.IsClient)
        {
            Logger.Error("Client created unmanaged {name}\n"
                + "Callstack: {callstack}", typeof(BesiegerCamp), Environment.StackTrace);
            return;
        }

        var message = CreateInstanceCreatedEventFast(__instance, __originalMethod.DeclaringType);

        ContainerProvider.TryResolve<IMessageBroker>(out var messageBroker);
        messageBroker?.Publish(__instance, message);
    }

    internal static void DestroyPostfix(ref object __instance, MethodBase __originalMethod)
    {
        // Call original if we call this function
        if (CallPolicy.IsOriginalAllowed()) return;

        if (ContainerProvider.TryResolve<IGameInterfaceConfig>(out var config) == false) return;

        if (config.IsClient)
        {
            Logger.Error("Client destroyed unmanaged {name}\n"
                + "Callstack: {callstack}", typeof(BesiegerCamp), Environment.StackTrace);
            return;
        }

        var destroyMessage = CreateInstanceDestroyedEventFast(__instance, __originalMethod.DeclaringType);

        ContainerProvider.TryResolve<IMessageBroker>(out var messageBroker);
messageBroker?.Publish(__instance, destroyMessage);
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
}