using Common;
using Common.Util;
using GameInterface.Registry;
using GameInterface.Registry.Auto;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.TownMarketDatas;


internal class TownMarketDataRegistry : IAutoRegistry<TownMarketData>
{
    ILogger Logger { get; }

    public TownMarketDataRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory)
    {
        Logger = logger;

        autoRegistryFactory.RegisterType(this);
    }

    public IEnumerable<MethodBase> Constructors => Array.Empty<MethodBase>();

    public IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public void RegisterAllObjects(IRegistry<TownMarketData> registry)
    {
        foreach (var town in Campaign.Current._towns)
        {
            registry.RegisterExistingObject(town.StringId, town._marketData);
        }
    }

    public void OnClientCreated(TownMarketData obj, string id)
    {
    }

    public void OnClientDestroyed(TownMarketData obj, string id)
    {
    }

    public void OnServerCreated(TownMarketData obj, string id)
    {
    }

    public void OnServerDestroyed(TownMarketData obj, string id)
    {
    }
}