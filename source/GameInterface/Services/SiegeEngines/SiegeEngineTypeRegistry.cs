using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
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

    public override IEnumerable<MethodBase> Constructors => AccessTools.GetDeclaredConstructors(typeof(SiegeEngineType));

    public override IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public override void RegisterAllObjects()
    {
        foreach (var siegeEvent in SiegeContainerLookup.ActiveSieges())
        {
            var settlement = siegeEvent.BesiegedSettlement;

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
