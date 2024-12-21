using Common.Messaging;
using TaleWorlds.Core;

namespace GameInterface.Services.WeaponDesigns.Messages.Collection;

/// <summary>
/// Internal event for <see cref="Crafting._history"/>
/// </summary>
public record WeaponDesignRemoved : IEvent
{
    public WeaponDesignRemoved(Crafting crafting, WeaponDesign weaponDesign)
    {
        Crafting = crafting;
        WeaponDesign = weaponDesign;
    }

    public Crafting Crafting { get; }
    public WeaponDesign WeaponDesign { get; }
}