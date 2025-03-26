using Common;
using GameInterface.Registry;
using GameInterface.Registry.Auto;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace GameInterface.Services.SettlementComponents;
internal class SettlementComponentRegistry : IAutoRegistry<SettlementComponent>
{
    ILogger Logger { get; }

    public SettlementComponentRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory)
    {
        Logger = logger;

        autoRegistryFactory.RegisterType(this);
    }

    public IEnumerable<MethodBase> Constructors => new MethodBase[]
    {
        AccessTools.Constructor(typeof(Town)),
        AccessTools.Constructor(typeof(Village)),
        AccessTools.Constructor(typeof(Hideout)),
    };

    public IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public void RegisterAllObjects(IRegistry<SettlementComponent> registry)
    {
        var settlementComponents = new List<SettlementComponent>();

        settlementComponents.AddRange(Town.AllFiefs);
        settlementComponents.AddRange(Village.All);
        settlementComponents.AddRange(Hideout.All);

        foreach (var settlementComponent in settlementComponents.DistinctBy(comp => comp.StringId))
        {
            var networkId = $"{nameof(SettlementComponent)}_{settlementComponent.StringId}";
            registry.RegisterExistingObject(networkId, settlementComponent);
        }
    }

    public void OnClientCreated(SettlementComponent obj, string id)
    {
    }

    public void OnClientDestroyed(SettlementComponent obj, string id)
    {
    }

    public void OnServerCreated(SettlementComponent obj, string id)
    {
    }

    public void OnServerDestroyed(SettlementComponent obj, string id)
    {
    }
}
