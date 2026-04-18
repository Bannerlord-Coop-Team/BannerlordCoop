using GameInterface.Registry;
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
    private int InstanceCounter = 0;

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
        typeof(PatrolPartyComponent),
        typeof(VillagerPartyComponent),
    };

    public override void RegisterAll()
    {
        foreach (var party in MobileParty.All)
        {
            var networkId = $"{party.PartyComponent.GetType().Name}_{party.StringId}";
            RegisterExistingObject(networkId, party.PartyComponent);
        }
    }

    protected override string GetNewId(PartyComponent party)
    {
        return $"{PartyComponentIdPrefix}_{Interlocked.Increment(ref InstanceCounter)}";
    }
}

