using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using Serilog;
using System;
using System.Runtime.CompilerServices;
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

        var message = CreateInstanceCreatedEventFast(__instance);

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

        var message = CreateInstanceDestroyedEventFast(__instance);

        MessageBroker.Instance.Publish(__instance, message);

        return true;
    }

    private static ConditionalWeakTable<object, Type> TypeWeakDictionary = new ConditionalWeakTable<object, Type>();
    public static IEvent CreateInstanceCreatedEventFast(object obj)
    {
        var type = obj.GetType();
        Type genericTypeDefinition;
        if (TypeWeakDictionary.TryGetValue(type, out genericTypeDefinition) == false)
        {
            genericTypeDefinition = typeof(InstanceCreated<>).MakeGenericType(type);
            TypeWeakDictionary.Add(type, genericTypeDefinition);

        }

        return (IEvent)Activator.CreateInstance(genericTypeDefinition, obj);
    }


    public static IEvent CreateInstanceDestroyedEventFast(object obj)
    {
        var type = obj.GetType();
        Type genericTypeDefinition;
        if (TypeWeakDictionary.TryGetValue(type, out genericTypeDefinition) == false)
        {
            genericTypeDefinition = typeof(InstanceDestroyed<>).MakeGenericType(type);
            TypeWeakDictionary.Add(type, genericTypeDefinition);

        }

        return (IEvent)Activator.CreateInstance(genericTypeDefinition, obj);
    }
}