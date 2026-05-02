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
    public readonly string PartyBaseId;
    public readonly string ItemId;
    public readonly string ItemModifierId;
    public readonly int Amount;

    public UpdateItemRoster(string partyBaseId, string itemId, string itemModifierId, int amount)
    {
        PartyBaseId = partyBaseId;
        ItemId = itemId;
        ItemModifierId = itemModifierId;
        Amount = amount;
    }
}
