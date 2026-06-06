using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.SiegeStrategies;
internal class SiegeStrategyRegistry : AutoRegistryBase<SiegeStrategy>
{
    public SiegeStrategyRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory, IObjectManager objectManager)
        : base(logger, autoRegistryFactory, objectManager)
    {
    }

    public override IEnumerable<MethodBase> Constructors => AccessTools.GetDeclaredConstructors(typeof(SiegeStrategy));

    public override IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public override void RegisterAllObjects()
    {
        foreach (var siegeStrategy in MBObjectManager.Instance.GetObjectTypeList<SiegeStrategy>())
        {
            if (string.IsNullOrEmpty(siegeStrategy.StringId))
            {
                Logger.Error("Unable to register {Type}: StringId is null/empty", nameof(SiegeStrategy));
                continue;
            }

            RegisterExistingObject(siegeStrategy.StringId, siegeStrategy);
        }
    }

    public override void OnClientCreated(SiegeStrategy obj, string id)
    {
    }

    public override void OnClientDestroyed(SiegeStrategy obj, string id)
    {
    }

    public override void OnServerCreated(SiegeStrategy obj, string id)
    {
    }

    public override void OnServerDestroyed(SiegeStrategy obj, string id)
    {
    }
}
