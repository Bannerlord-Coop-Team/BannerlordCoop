using TaleWorlds.Core;

namespace Missions.Tournaments.Spectators;

public static class TournamentSpectatorOrange
{
    public const string ItemId = "coop_spectator_orange";
    public const short RefillAmount = 1;
    public const EquipmentIndex EquipmentSlot = EquipmentIndex.Weapon0;

    public static Equipment BuildEquipment(Equipment civilianEquipment, ItemObject orangeItem)
    {
        var equipment = civilianEquipment == null
            ? new Equipment(Equipment.EquipmentType.Civilian)
            : new Equipment(civilianEquipment);
        for (int i = (int)EquipmentIndex.WeaponItemBeginSlot;
             i < (int)EquipmentIndex.NumAllWeaponSlots;
             i++)
        {
            equipment[(EquipmentIndex)i] = default;
        }

        if (orangeItem != null)
            equipment[EquipmentSlot] = new EquipmentElement(orangeItem);
        return equipment;
    }

    public static bool ShouldRefill(ItemObject equippedItem, ItemObject orangeItem, short amount)
        => equippedItem != null &&
           orangeItem != null &&
           ReferenceEquals(equippedItem, orangeItem) &&
           amount < RefillAmount;
    public static bool ShouldBlockPickup(bool isSpectator, bool isOrange)
        => isSpectator || isOrange;

    public static bool ShouldBlockDrop(bool isSpectator)
        => isSpectator;

    public static bool ShouldDisappearOnCollision(bool isSpectator, bool isOrange)
        => isSpectator && isOrange;
}
