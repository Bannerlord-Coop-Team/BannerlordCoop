using Common;
using GameInterface.Registry.Auto;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.VillageTypes;
internal class VillageTypeRegistry : IAutoRegistry<VillageType>
{
    ILogger Logger { get; }
    public VillageTypeRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory)
    {
        Logger = logger;

        autoRegistryFactory.RegisterType(this);
    }

    public IEnumerable<MethodBase> Constructors => AccessTools.GetDeclaredConstructors(typeof(VillageType));

    public IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public void RegisterAllObjects(IRegistry<VillageType> registry)
    {
        foreach (var villageType in VillageType.All)
        {
            registry.RegisterExistingObject(villageType.StringId, villageType);
        }
    }

    public void OnClientCreated(VillageType obj, string id)
    {
    }

    public void OnClientDestroyed(VillageType obj, string id)
    {
    }

    public void OnServerCreated(VillageType obj, string id)
    {
    }

    public void OnServerDestroyed(VillageType obj, string id)
    {
    }
}
