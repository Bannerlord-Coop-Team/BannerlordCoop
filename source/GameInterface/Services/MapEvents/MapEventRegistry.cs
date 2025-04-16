using System;
using System.Collections.Generic;
using System.Reflection;
using Common;
using GameInterface.Registry.Auto;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.MapEvents;

/// <summary>
/// Registry for <see cref="MapEvent"/> objects
/// </summary>
internal class MapEventRegistry : IAutoRegistry<MapEvent>
{
    ILogger Logger { get; }

    public MapEventRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory)
    {
        Logger = logger;

        autoRegistryFactory.RegisterType(this);
    }

    public IEnumerable<MethodBase> Constructors => new MethodBase[]
    {
        AccessTools.Constructor(typeof(MapEvent))
    };

    public IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public void RegisterAllObjects(IRegistry<MapEvent> registry)
    {
        foreach(var instance in Campaign.Current.MapEventManager.MapEvents)
        {
            if (registry.RegisterNewObject(instance, out var _) == false) Logger.Error($"Unable to register {nameof(MapEvent)}");
        }
    }

    public void OnClientCreated(MapEvent obj, string id)
    {
        
    }

    public void OnClientDestroyed(MapEvent obj, string id)
    {
        
    }

    public void OnServerCreated(MapEvent obj, string id)
    {
        
    }

    public void OnServerDestroyed(MapEvent obj, string id)
    {
        
    }
}

