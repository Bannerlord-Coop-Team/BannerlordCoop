using Common;
using GameInterface.Registry;
using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.VillageTypes;
internal class VillageTypeRegistry : AutoRegistryBase<VillageType>
{
    public VillageTypeRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory, IObjectManager objectManager)
        : base(logger, autoRegistryFactory, objectManager)
    {
    }

    public override IEnumerable<MethodBase> Constructors => AccessTools.GetDeclaredConstructors(typeof(VillageType));

    public override IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public override void RegisterAllObjects()
    {
        foreach (var villageType in VillageType.All)
        {
            RegisterExistingObject(villageType.StringId, villageType);
        }
    }

    public override void OnClientCreated(VillageType obj, string id)
    {
    }

    public override void OnClientDestroyed(VillageType obj, string id)
    {
    }

    public override void OnServerCreated(VillageType obj, string id)
    {
    }

    public override void OnServerDestroyed(VillageType obj, string id)
    {
    }
}
