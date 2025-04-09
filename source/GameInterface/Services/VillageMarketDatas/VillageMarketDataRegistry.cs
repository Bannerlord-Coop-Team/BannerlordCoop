using System;
using System.Collections.Generic;
using System.Reflection;
using Common;
using GameInterface.Registry.Auto;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.VillageMarketDatas;
/// <summary>
/// Registry manager for VillageMarketData
/// </summary>
internal class VillageMarketRegistry : IAutoRegistry<VillageMarketData>
{
    ILogger Logger { get; }

    public IEnumerable<MethodBase> Constructors => new MethodBase[] {
        AccessTools.Constructor(typeof(VillageMarketData), new Type[] { typeof(Village) })
    };

    public IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public VillageMarketRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory)
    {
        Logger = logger;

        autoRegistryFactory.RegisterType(this);
    }

    public void RegisterAllObjects(IRegistry<VillageMarketData> registry)
    {
        foreach (var village in Campaign.Current._villages)
        {
            var networkId = $"{nameof(VillageMarketData)}_{village.StringId}";
            if (registry.RegisterExistingObject(networkId, village.MarketData) == false)
                Logger.Error($"Unable to register {village.MarketData}");
        }
    }

    public void OnClientCreated(VillageMarketData obj, string id)
    {
        
    }

    public void OnClientDestroyed(VillageMarketData obj, string id)
    {
        
    }

    public void OnServerCreated(VillageMarketData obj, string id)
    {
        
    }

    public void OnServerDestroyed(VillageMarketData obj, string id)
    {
        
    }
}