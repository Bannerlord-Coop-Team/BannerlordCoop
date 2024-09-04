using GameInterface.Services.Registry;
using System;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.MapEvents;

/// <summary>
/// Registry for <see cref="MapEventSide"/> objects
/// </summary>
internal class MapEventSideRegistry : RegistryBase<MapEventSide>
{
    public MapEventSideRegistry(IRegistryCollection collection) : base(collection) { }

    public override void RegisterAll()
    {
        // Not required
    }

    protected override string GetNewId(MapEventSide party)
    {
        return Guid.NewGuid().ToString();
    }
}

