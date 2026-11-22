using Common.Messaging;
using GameInterface.Services.Inventory.Data;
using ProtoBuf;

namespace GameInterface.Services.Inventory.Messages;

/// <summary>
/// Sent by the server to the trading player once their inventory screen's trade is applied, so their game raises
/// the player inventory exchange event with what the server actually moved.
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkTradeCompleted : ICommand
{
    [ProtoMember(1)]
    public readonly (ItemRosterElementData, int)[] PurchasedItems;
    [ProtoMember(2)]
    public readonly (ItemRosterElementData, int)[] SoldItems;
    [ProtoMember(3)]
    public readonly bool IsTrading;

    public NetworkTradeCompleted(
        (ItemRosterElementData, int)[] purchasedItems,
        (ItemRosterElementData, int)[] soldItems,
        bool isTrading)
    {
        PurchasedItems = purchasedItems;
        SoldItems = soldItems;
        IsTrading = isTrading;
    }
}
