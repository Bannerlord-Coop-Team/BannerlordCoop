using Common.Messaging;

namespace GameInterface.Services.ItemRosters.Messages;

/// <summary>
/// Called when an ItemRoster should be updated.
/// </summary>
public readonly struct UpdateItemRoster : ICommand
{
    public readonly uint ItemRosterId;
    public readonly uint ItemId;
    public readonly uint ItemModifierId;
    public readonly int Amount;

    public UpdateItemRoster(uint itemRosterId, uint itemId, uint itemModifierId, int amount)
    {
        ItemRosterId = itemRosterId;
        ItemId = itemId;
        ItemModifierId = itemModifierId;
        Amount = amount;
    }
}
