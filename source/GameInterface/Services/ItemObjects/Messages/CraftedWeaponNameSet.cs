using Common.Messaging;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace GameInterface.Services.ItemObjects.Messages;
public readonly struct SetCraftedWeaponName : ICommand
{
    public readonly ItemObject Weapon;
    public readonly TextObject Name;

    public SetCraftedWeaponName(ItemObject weapon, TextObject name)
    {
        Weapon = weapon;
        Name = name;
    }
}