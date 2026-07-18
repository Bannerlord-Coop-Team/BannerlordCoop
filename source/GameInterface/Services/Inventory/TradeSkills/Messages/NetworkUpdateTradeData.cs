using Common.Messaging;
using ProtoBuf;
using System;
using System.Collections.Generic;
using TaleWorlds.Core;

namespace GameInterface.Services.Inventory.TradeSkills.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkUpdateTradeData : ICommand
{
    [ProtoMember(1)]
    public readonly string PlayerHeroId;

    [ProtoMember(2)]
    public readonly List<ValueTuple<ItemRosterElement, int>> PurchasedItems;

    [ProtoMember(3)]
    public readonly List<ValueTuple<ItemRosterElement, int>> SoldItems;

    [ProtoMember(4)]
    public readonly bool IsTrading;

    public NetworkUpdateTradeData(
        string playerHeroId,
        List<(ItemRosterElement, int)> purchasedItems,
        List<(ItemRosterElement, int)> soldItems,
        bool isTrading)
    {
        PlayerHeroId = playerHeroId;
        PurchasedItems = purchasedItems;
        SoldItems = soldItems;
        IsTrading = isTrading;
    }
}
