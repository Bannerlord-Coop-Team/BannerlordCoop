using GameInterface.Registry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace GameInterface.Services.SettlementComponents;
internal class SettlementComponentRegistry : RegistryBase<SettlementComponent>
{
    private const string SettlementComponentIdPrefix = "CoopSettlementComponent";
    private int InstanceCounter = 0;

    public SettlementComponentRegistry(IRegistryCollection collection) : base(collection)
    {
    }

    public override void RegisterAll()
    {
        List<SettlementComponent> settlementComponents = new List<SettlementComponent>();

        settlementComponents.AddRange(Town.AllFiefs);
        settlementComponents.AddRange(Village.All);
        settlementComponents.AddRange(Hideout.All);

        foreach (var settlementComponent in settlementComponents.DistinctBy(comp => comp.StringId))
        {
            var networkId = $"{nameof(SettlementComponent)}_{settlementComponent.StringId}";
            RegisterExistingObject(networkId, settlementComponent);
        }
    }

    protected override string GetNewId(SettlementComponent obj)
    {
        return $"{SettlementComponentIdPrefix}_{Interlocked.Increment(ref InstanceCounter)}";
    }
}
