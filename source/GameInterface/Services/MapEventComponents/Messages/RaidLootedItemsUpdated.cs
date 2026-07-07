using Common.Messaging;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.MapEventComponents.Messages;

internal readonly struct RaidLootedItemsUpdated : IEvent
{
    public readonly MobileParty MobileParty;
    public readonly ItemRoster LootedItems;

    public RaidLootedItemsUpdated(MobileParty mobileParty, ItemRoster lootedItems)
    {
        MobileParty = mobileParty;
        LootedItems = lootedItems;
    }
}
