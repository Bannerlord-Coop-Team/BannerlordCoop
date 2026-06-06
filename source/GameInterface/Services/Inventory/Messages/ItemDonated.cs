using Common.Messaging;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;

namespace GameInterface.Services.Inventory.Messages;

public readonly struct ItemDonated : IEvent
{
    public readonly ItemRoster TargetItemRoster;
    public readonly EquipmentElement EquipmentElement;
    public readonly PartyBase Party;
    public readonly TroopRosterElement TroopRosterElement;
    public readonly int GainedXp;

    public ItemDonated(
        ItemRoster targetItemRoster,
        EquipmentElement equipmentElement,
        PartyBase party,
        TroopRosterElement troopRosterElement,
        int gainedXp)
    {
        TargetItemRoster = targetItemRoster;
        EquipmentElement = equipmentElement;
        Party = party;
        TroopRosterElement = troopRosterElement;
        GainedXp = gainedXp;
    }
}
