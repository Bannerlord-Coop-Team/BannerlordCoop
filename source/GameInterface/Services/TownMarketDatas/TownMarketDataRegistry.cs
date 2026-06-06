using Common;
using Common.Util;
using GameInterface.Registry;
using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.TownMarketDatas;


internal class TownMarketDataRegistry : AutoRegistryBase<TownMarketData>
{
    public TownMarketDataRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory, IObjectManager objectManager)
        : base(logger, autoRegistryFactory, objectManager)
    {
    }

    public override IEnumerable<MethodBase> Constructors => Array.Empty<MethodBase>();

    public override IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public override void RegisterAllObjects()
    {
        var towns = Campaign.Current?._towns;
        if (towns == null)
        {
            Logger.Error("Unable to register TownMarketData when Campaign towns is null");
            return;
        }

        foreach (var town in towns)
        {
            if (town == null) continue;
            if (town._marketData == null) continue;
            if (string.IsNullOrEmpty(town.StringId)) continue;

            RegisterExistingObject(town.StringId, town._marketData);
        }
    }

    public override void OnClientCreated(TownMarketData obj, string id)
    {
    }

    public override void OnClientDestroyed(TownMarketData obj, string id)
    {
    }

    public override void OnServerCreated(TownMarketData obj, string id)
    {
    }

    public override void OnServerDestroyed(TownMarketData obj, string id)
    {
    }
}