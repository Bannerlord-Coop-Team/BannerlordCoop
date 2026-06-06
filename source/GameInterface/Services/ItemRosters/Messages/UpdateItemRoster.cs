using Common.Logging.Attributes;
using Common.Messaging;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace GameInterface.Services.ItemRosters.Messages;

/// <summary>
/// Called when an ItemRoster should be updated.
/// </summary>
[BatchLogMessage]
public readonly struct UpdateItemRoster : ICommand
{
    public readonly string ItemRosterId;
    public readonly string ItemId;
    public readonly string ItemModifierId;
    public readonly int Amount;

    public UpdateItemRoster(string itemRosterId, string itemId, string itemModifierId, int amount)
    {
        ItemRosterId = itemRosterId;
        ItemId = itemId;
        ItemModifierId = itemModifierId;
        Amount = amount;
    }
}
