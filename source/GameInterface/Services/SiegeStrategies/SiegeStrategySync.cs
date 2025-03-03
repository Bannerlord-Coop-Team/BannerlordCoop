using Common.Logging;
using GameInterface.AutoSync;
using GameInterface.Registry.Auto;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.ObjectSystem;
using static TaleWorlds.CampaignSystem.Settlements.Settlement;

namespace GameInterface.Services.SiegeStrategies;
internal class SiegeStrategySync : IAutoSync
{
    static readonly ILogger Logger = LogManager.GetLogger<SiegeStrategySync>();
    public SiegeStrategySync(IAutoRegistryFactory registryFactory)
    {
        // Lifetime
        var ctors = AccessTools.GetDeclaredConstructors(typeof(SiegeStrategy));
        registryFactory.TryRegisterType<SiegeStrategy>(ctors, RegisterAll);
    }

    void RegisterAll(AutoRegistry<SiegeStrategy> registry)
    {
        foreach (var siegeStrategy in MBObjectManager.Instance.GetObjectTypeList<SiegeStrategy>())
        {
            if (registry.RegisterNewObject(siegeStrategy, out var _) == false) Logger.Error($"Unable to register {nameof(SiegeStrategy)}");
        }
    }
}
