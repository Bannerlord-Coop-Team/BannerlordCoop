using GameInterface.Services.Registry;
using System;
using TaleWorlds.CampaignSystem.Siege;

namespace GameInterface.Services.Sieges;

/// <summary>
/// Registry for <see cref="SiegeEvent"/> objects
/// </summary>
internal class SeigeEventRegistry : RegistryBase<SiegeEvent>
{
    public SeigeEventRegistry(IRegistryCollection collection) : base(collection) { }

    public override void RegisterAll()
    {
        // Not required
    }

    protected override string GetNewId(SiegeEvent party)
    {
        return Guid.NewGuid().ToString();
    }
}