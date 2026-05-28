using Common.Messaging;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace GameInterface.Services.ItemObjects.Messages;
public record CraftedWeaponNameSet : ICommand
{
    public ItemObject Weapon;
    public TextObject Name;

    public CraftedWeaponNameSet(ItemObject weapon, TextObject name)
    {
        Weapon = weapon;
        Name = name;
    }
}