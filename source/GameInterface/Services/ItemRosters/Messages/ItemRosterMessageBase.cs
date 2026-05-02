using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace GameInterface.Services.ItemRosters.Messages;

public readonly struct ItemRosterMessageBase
{
    public readonly PartyBase PartyBase;
    public readonly ItemObject Item;
    public readonly ItemModifier ItemModifier;
    public readonly int Amount;

    public ItemRosterMessageBase(
        PartyBase partyBase,
        ItemObject item,
        ItemModifier itemModifier,
        int amount)
    {
        PartyBase = partyBase;
        Item = item;
        ItemModifier = itemModifier;
        Amount = amount;
    }
}