using Common;
using Common.Util;
using GameInterface.Registry.Auto;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Locations;
using TaleWorlds.Localization;

namespace GameInterface.Services.Settlements;
internal class SettlementRegistry : IAutoRegistry<Settlement>
{
    ILogger Logger { get; }
    public SettlementRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory)
    {
        Logger = logger;

        autoRegistryFactory.RegisterType(this);
    }

    public IEnumerable<MethodBase> Constructors => new MethodBase[] {
        AccessTools.Constructor(typeof(Settlement), new Type[] { typeof(TextObject), typeof(LocationComplex), typeof(PartyTemplateObject)}),
    };

    public IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public void RegisterAllObjects(IRegistry<Settlement> registry)
    {
        foreach (var settlement in Settlement.All.OrderBy(obj => obj.Id))
        {
            registry.RegisterExistingObject(settlement.StringId, settlement);
        }
    }

    public void OnClientCreated(Settlement obj, string id)
    {
        GameLoopRunner.RunOnMainThread(() =>
        {
            using (new AllowedThread())
            {
                obj.InitSettlement();
            }
        });
    }

    public void OnClientDestroyed(Settlement obj, string id)
    {
    }

    public void OnServerCreated(Settlement obj, string id)
    {
    }

    public void OnServerDestroyed(Settlement obj, string id)
    {
    }
}
