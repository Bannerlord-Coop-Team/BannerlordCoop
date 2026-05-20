using Common;
using GameInterface.Registry;
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

internal class SiegeEngineConstructionProgressRegistry : AutoRegistryBase<SiegeEngineConstructionProgress>
{
    public SiegeEngineConstructionProgressRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory, IObjectManager objectManager)
        : base(logger, autoRegistryFactory, objectManager)
    {
    }

    public override IEnumerable<MethodBase> Constructors => AccessTools.GetDeclaredConstructors(typeof(SiegeEngineConstructionProgress));

    public override IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public override void RegisterAllObjects()
    {
        var siegeEvents = Campaign.Current?.SiegeEventManager?.SiegeEvents;
        if (siegeEvents == null) return;

        foreach (var siegeEvent in siegeEvents)
        {
            var settlement = siegeEvent?.BesiegedSettlement;
            if (settlement == null) continue;

            var progress = siegeEvent?.BesiegerCamp?.SiegeEngines?.SiegePreparations;
            if (progress == null) continue;

            RegisterExistingObject(settlement.StringId, progress);
        }
    }

    public override void OnClientCreated(SiegeEngineConstructionProgress obj, string id)
    {
    }

    public override void OnClientDestroyed(SiegeEngineConstructionProgress obj, string id)
    {
    }

    public override void OnServerCreated(SiegeEngineConstructionProgress obj, string id)
    {
    }

    public override void OnServerDestroyed(SiegeEngineConstructionProgress obj, string id)
    {
    }
}