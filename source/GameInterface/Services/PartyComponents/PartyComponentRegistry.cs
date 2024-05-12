using GameInterface.Services.Registry;
using System;
using TaleWorlds.CampaignSystem.Party.PartyComponents;

namespace GameInterface.Services.PartyComponents;

/// <summary>
/// Registry for <see cref="PartyComponent"/> objects
/// </summary>
internal class PartyComponentRegistry : RegistryBase<PartyComponent>
{
    public PartyComponentRegistry(IRegistryCollection collection) : base(collection) { }

    public override void RegisterAll()
    {
        // Not required for party component
    }

    protected override string GetNewId(PartyComponent party)
    {
        return Guid.NewGuid().ToString();
    }
}

