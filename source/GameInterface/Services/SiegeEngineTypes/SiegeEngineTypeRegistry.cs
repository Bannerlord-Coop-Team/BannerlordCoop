using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.SiegeEngineTypes;

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
        foreach (var siegeEngineType in MBObjectManager.Instance.GetObjectTypeList<SiegeEngineType>())
        {
            if (string.IsNullOrEmpty(siegeEngineType.StringId))
            {
                Logger.Error("Unable to register {Type}: StringId is null/empty", nameof(SiegeEngineType));
                continue;
            }

            RegisterExistingObject(siegeEngineType.StringId, siegeEngineType);
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