using Common;
using GameInterface.Registry;
using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace GameInterface.Services.SiegeEngines;
internal class SiegeEngineTypeRegistry : AutoRegistryBase<SiegeEngineType>
{
    public SiegeEngineTypeRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory, IObjectManager objectManager)
        : base(logger, autoRegistryFactory, objectManager)
    {
    }

    public override IEnumerable<MethodBase> Constructors => Array.Empty<MethodBase>();

    public override IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public override void RegisterAllObjects()
    {
        var siegeEvents = Campaign.Current?.SiegeEventManager?.SiegeEvents;
        if (siegeEvents == null)
        {
            Logger.Error("Unable to register siege engine types when SiegeEvents is null");
            return;
        }

        foreach (var siegeEvent in siegeEvents)
        {
            var settlement = siegeEvent?.BesiegedSettlement;
            if (settlement == null)
            {
                Logger.Error("Unable to register siege engine type: BesiegedSettlement is null");
                continue;
            }

            var siegeEngine = siegeEvent?.BesiegerCamp?.SiegeEngines?.SiegePreparations?.SiegeEngine;
            if (siegeEngine == null)
            {
                Logger.Error("Unable to register siege engine type for settlement {SettlementId}: SiegeEngine is null", settlement.StringId);
                continue;
            }

            if (string.IsNullOrEmpty(settlement.StringId))
            {
                Logger.Error("Unable to register siege engine type: settlement StringId is null/empty");
                continue;
            }

            RegisterExistingObject(settlement.StringId, siegeEngine);
        }
    }

    public override void OnClientCreated(SiegeEngineType obj, string id)
    {
    }

    public override void OnClientDestroyed(SiegeEngineType obj, string id)
    {
    }

    public override void OnServerCreated(SiegeEngineType obj, string id)
    {
    }

    public override void OnServerDestroyed(SiegeEngineType obj, string id)
    {
    }
}
