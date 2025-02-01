using GameInterface.Services.Registry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;

namespace GameInterface.Services.PartyComponents;

/// <summary>
/// Registry for <see cref="PartyComponent"/> objects
/// </summary>
internal class PartyComponentRegistry : RegistryBase<PartyComponent>
{
    private const string PartyComponentIdPrefix = "CoopPartyComponent";
    private static int InstanceCounter = 0;

    public PartyComponentRegistry(IRegistryCollection collection) : base(collection) { }

    public override IEnumerable<Type> ManagedTypes { get; } = new Type[]
    {
        typeof(PartyComponent),
        typeof(WarPartyComponent),
        typeof(BanditPartyComponent),
        typeof(CustomPartyComponent),
        typeof(CaravanPartyComponent),
        typeof(GarrisonPartyComponent),
        typeof(LordPartyComponent),
        typeof(MilitiaPartyComponent),
        typeof(VillagerPartyComponent),
    };

    public override void RegisterAll()
    {
        foreach (var component in MobileParty.All.Select(p => p.PartyComponent).Where(c => c != null))
        {
            RegisterNewObject(component, out var _);
        }
    }

    protected override string GetNewId(PartyComponent party)
    {
        return $"{PartyComponentIdPrefix}_{Interlocked.Increment(ref InstanceCounter)}";

    }
}

