using Common.Messaging;
using TaleWorlds.Localization;

namespace GameInterface.Services.Smithing.Messages;

public readonly struct SetBehaviorCraftedWeaponName : IEvent
{
    public readonly string CraftedWeaponId;
    public readonly TextObject Name;

    public SetBehaviorCraftedWeaponName(string craftedWeaponId, TextObject name)
    {
        CraftedWeaponId = craftedWeaponId;
        Name = name;
    }
}