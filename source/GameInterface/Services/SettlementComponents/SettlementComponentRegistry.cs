using GameInterface.Services.Registry;
using System;
using System.Collections.Generic;
using System.Threading;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.SettlementComponents;
internal class SettlementComponentRegistry : RegistryBase<SettlementComponent>
{
    private const string SettlementComponentIdPrefix = "CoopSettlementComponent";
    private static int InstanceCounter = 0;

    public override IEnumerable<Type> ManagedTypes { get; } = new Type[]
    {
        typeof(SettlementComponent),
        typeof(Fief),
        typeof(Town),
        typeof(Village),
        typeof(Hideout),
        typeof(RetirementSettlementComponent),
    };

    public SettlementComponentRegistry(IRegistryCollection collection) : base(collection)
    {
    }

    public override void RegisterAll()
    {
        List<SettlementComponent> settlementComponents = new List<SettlementComponent>();

        settlementComponents.AddRange(Town.AllFiefs);
        settlementComponents.AddRange(Village.All);
        settlementComponents.AddRange(Hideout.All);

        foreach (var settlement in settlementComponents)
        {
            RegisterExistingObject(settlement.StringId, settlement);
        }
    }

    protected override string GetNewId(SettlementComponent obj)
    {
        return $"{SettlementComponentIdPrefix}_{Interlocked.Increment(ref InstanceCounter)}";
    }
}
