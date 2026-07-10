using Common.Util;
using Helpers;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;

namespace GameInterface.Services.ItemRosters.Interfaces;

public interface IItemRosterInterface : IGameAbstraction
{
    ItemRoster GetItemRosterFromData(ItemRosterElement[] itemRosterData);
    void OpenPartyLootScreen(MobileParty encounterParty, out bool partyHasItems, out ItemRosterElement[] itemRosterElements);
}

public class ItemRosterInterface : IItemRosterInterface
{
    public ItemRoster GetItemRosterFromData(ItemRosterElement[] itemRosterData)
    {
        ItemRoster itemRoster = new();
        if (itemRosterData != null) // Empty array transferred as null object, guard against NRE if empty
        {
            foreach (var itemRosterElement in itemRosterData)
            {
                itemRoster.Add(itemRosterElement);
            }
        }

        return itemRoster;
    }

    public void OpenPartyLootScreen(MobileParty encounterParty, out bool partyHasItems, out ItemRosterElement[] itemRosterElements)
    {
        ItemRoster itemRoster = null;
        using (new AllowedThread())
        {
            itemRoster = new ItemRoster(encounterParty.ItemRoster);

            itemRosterElements = itemRoster._data;

            partyHasItems = false;
            for (int i = 0; i < itemRoster.Count; i++)
            {
                if (itemRoster.GetElementNumber(i) > 0)
                {
                    partyHasItems = true;
                    break;
                }
            }
            if (partyHasItems)
            {
                InventoryScreenHelper.OpenScreenAsLoot(new Dictionary<PartyBase, ItemRoster>
                {
                    { PartyBase.MainParty, itemRoster }
                });
            }
        }
    }
}