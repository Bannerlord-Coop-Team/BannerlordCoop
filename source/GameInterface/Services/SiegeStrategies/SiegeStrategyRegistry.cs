using Common;
using Common.Logging;
using GameInterface.AutoSync;
using GameInterface.Registry.Auto;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.ObjectSystem;
using static TaleWorlds.CampaignSystem.Settlements.Settlement;

namespace GameInterface.Services.SiegeStrategies;
internal class SiegeStrategyRegistry : IAutoRegistry<SiegeStrategy>
{
    ILogger Logger { get; }
    public SiegeStrategyRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory)
    {
        Logger = logger;

        autoRegistryFactory.RegisterType(this);
    }

    public IEnumerable<MethodBase> Constructors => AccessTools.GetDeclaredConstructors(typeof(SiegeStrategy));

    public IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public void RegisterAllObjects(IRegistry<SiegeStrategy> registry)
    {
        foreach (var siegeStrategy in MBObjectManager.Instance.GetObjectTypeList<SiegeStrategy>())
        {
            if (registry.RegisterNewObject(siegeStrategy, out var _) == false) Logger.Error($"Unable to register {nameof(SiegeStrategy)}");
        }
    }

    public void OnClientCreated(SiegeStrategy obj, string id)
    {
    }

    public void OnClientDestroyed(SiegeStrategy obj, string id)
    {
    }

    public void OnServerCreated(SiegeStrategy obj, string id)
    {
    }

    public void OnServerDestroyed(SiegeStrategy obj, string id)
    {
    }
}
