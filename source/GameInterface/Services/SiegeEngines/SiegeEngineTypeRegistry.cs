using Common;
using GameInterface.Registry.Auto;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace GameInterface.Services.SiegeEngines;
internal class SiegeEngineTypeRegistry : IAutoRegistry<SiegeEngineType>
{
    ILogger Logger { get; }
    public SiegeEngineTypeRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory)
    {
        Logger = logger;

        autoRegistryFactory.RegisterType(this);
    }

    public IEnumerable<MethodBase> Constructors => Array.Empty<MethodBase>();

    public IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public void RegisterAllObjects(IRegistry<SiegeEngineType> registry)
    {
        foreach (var siegeEngine in Campaign.Current.SiegeEventManager.SiegeEvents
                .Select(siegeEvent => siegeEvent.BesiegerCamp.SiegeEngines.SiegePreparations.SiegeEngine))
        {
            registry.RegisterNewObject(siegeEngine, out _);
        }
    }

    public void OnClientCreated(SiegeEngineType obj, string id)
    {
    }

    public void OnClientDestroyed(SiegeEngineType obj, string id)
    {
    }

    public void OnServerCreated(SiegeEngineType obj, string id)
    {
    }

    public void OnServerDestroyed(SiegeEngineType obj, string id)
    {
    }
}
