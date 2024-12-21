using Common.Messaging;
using TaleWorlds.Core;

namespace GameInterface.Services.WeaponDesigns.Messages.Collection;

/// <summary>
/// Internal event for <see cref="Crafting._history" />
/// </summary>
public record WeaponDesignAdded : IEvent
{
    public WeaponDesignAdded(Crafting crafting, WeaponDesign weaponDesign)
    {
        Crafting = crafting;
        WeaponDesign = weaponDesign;
    }

    public Crafting Crafting { get; }
    public WeaponDesign WeaponDesign { get; }
}