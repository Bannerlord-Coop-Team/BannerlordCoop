using GameInterface.Services.Registry;
using System;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.MapEvents;

/// <summary>
/// Registry for <see cref="MapEvent"/> objects
/// </summary>
internal class MapEventRegistry : RegistryBase<MapEvent>
{
    public MapEventRegistry(IRegistryCollection collection) : base(collection) { }

    public override void RegisterAll()
    {
        // Not required
    }

    protected override string GetNewId(MapEvent party)
    {
        return Guid.NewGuid().ToString();
    }
}

