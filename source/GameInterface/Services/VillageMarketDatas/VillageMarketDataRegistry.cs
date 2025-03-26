using Common;
using GameInterface.Registry.Auto;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.VillageMarketDatas;


/// <summary>
/// Registry manager for SiegeEvent
/// </summary>
internal class VillageMarketRegistry : IAutoRegistry<VillageMarketData>
{
    ILogger Logger { get; }
    public VillageMarketRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory)
    {
        Logger = logger;

        autoRegistryFactory.RegisterType(this);
    }

    public IEnumerable<MethodBase> Constructors => AccessTools.GetDeclaredConstructors(typeof(VillageMarketData));

    public IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public void RegisterAllObjects(IRegistry<VillageMarketData> registry)
    {
        foreach (var village in Campaign.Current._villages)
        {
            var networkId = $"{nameof(VillageMarketData)}_{village.StringId}";
            registry.RegisterExistingObject(networkId, village._marketData);
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