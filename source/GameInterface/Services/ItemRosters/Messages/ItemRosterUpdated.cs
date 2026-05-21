using Common.Logging.Attributes;
using Common.Messaging;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;

namespace GameInterface.Services.ItemRosters.Messages;

/// <summary> 
/// Called when an ItemRoster is updated.
/// </summary>
[BatchLogMessage]
public readonly struct ItemRosterUpdated : IEvent
{
    public readonly ItemRoster Instance;
    public readonly ItemObject Item;
    public readonly ItemModifier ItemModifier;
    public readonly int Amount;

    public ItemRosterUpdated(
        ItemRoster instance,
        ItemObject item,
        ItemModifier itemModifier,
        int amount)
    {
        Instance = instance;
        Item = item;
        ItemModifier = itemModifier;
        Amount = amount;
    }
}
