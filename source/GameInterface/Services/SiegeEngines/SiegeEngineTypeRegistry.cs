using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

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
        // The whole XML catalog, keyed by StringId (deterministic on every machine): consumers resolve
        // any engine type by co-op id — a firing catapult in a bombardment notification just as much as
        // the prep-slot Preparations type — so registering only the active sieges' prep slots left every
        // other type unresolvable ("Failed to get id" on each bombardment broadcast).
        foreach (var engineType in MBObjectManager.Instance.GetObjectTypeList<SiegeEngineType>())
        {
            RegisterExistingObject(engineType.StringId, engineType);
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
