using Common;
using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using static TaleWorlds.CampaignSystem.Siege.SiegeEvent;

namespace GameInterface.Services.SiegeEngineConstructionProgressService;

internal class SiegeEngineConstructionProgressRegistry : IAutoRegistry<SiegeEngineConstructionProgress>
{
    ILogger Logger { get; }
    public SiegeEngineConstructionProgressRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory)
    {
        Logger = logger;

        autoRegistryFactory.RegisterType(this);
    }

    public IEnumerable<MethodBase> Constructors => AccessTools.GetDeclaredConstructors(typeof(SiegeEngineConstructionProgress));

    public IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public void RegisterAllObjects(IObjectManager objectManager)
    {
        foreach (var siegeEngineConstructionProgress in Campaign.Current.SiegeEventManager.SiegeEvents
            .Select(siegeEvent => siegeEvent.BesiegerCamp.SiegeEngines.SiegePreparations))
        {
            objectManager.AddNewObject(siegeEngineConstructionProgress, out _);
        }
    }

    public void OnClientCreated(SiegeEngineConstructionProgress obj, string id)
    {
    }

    public void OnClientDestroyed(SiegeEngineConstructionProgress obj, string id)
    {
    }

    public void OnServerCreated(SiegeEngineConstructionProgress obj, string id)
    {
    }

    public void OnServerDestroyed(SiegeEngineConstructionProgress obj, string id)
    {
    }
}