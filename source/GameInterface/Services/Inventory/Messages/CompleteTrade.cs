using Common.Messaging;
using GameInterface.Services.Inventory.Data;
using ProtoBuf;

namespace GameInterface.Services.Inventory.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct CompleteTrade : ICommand
{
    [ProtoMember(1)]
    public readonly string FromItemRosterId;
    [ProtoMember(2)]
    public readonly bool IsFromItemRosterNull;
    [ProtoMember(3)]
    public readonly string ToItemRosterId;
    [ProtoMember(4)]
    public readonly bool IsTrading;
    [ProtoMember(5)]
    public readonly bool IsDonating;
    [ProtoMember(6)]
    public readonly string HeroId;
    [ProtoMember(7)]
    public readonly int TotalAmount;
    [ProtoMember(8)]
    public readonly int MerchantGold;
    [ProtoMember(9)]
    public readonly string PartyId;
    [ProtoMember(11)]
    public readonly bool IsSettlementComponentNull;
    [ProtoMember(12)]
    public readonly string CurrentSettlementComponentId;

    [ProtoMember(13)]
    public readonly (ItemRosterElementData, int)[] BoughtItems;
    [ProtoMember(14)]
    public readonly (ItemRosterElementData, int)[] SoldItems;

    public CompleteTrade(
        string fromItemRosterId,
        bool isFromItemRosterNull,
        string toItemRosterId,
        bool isTrading,
        bool isDonating,
        string heroId,
        int totalAmount,
        int merchantGold,
        string partyId,
        bool isSettlementComponentNull,
        string currentSettlementComponentId,
        (ItemRosterElementData, int)[] boughtItems,
        (ItemRosterElementData, int)[] soldItems)
    {
        FromItemRosterId = fromItemRosterId;
        IsFromItemRosterNull = isFromItemRosterNull;
        ToItemRosterId = toItemRosterId;
        IsTrading = isTrading;
        IsDonating = isDonating;
        HeroId = heroId;
        TotalAmount = totalAmount;
        MerchantGold = merchantGold;
        PartyId = partyId;
        IsSettlementComponentNull = isSettlementComponentNull;
        CurrentSettlementComponentId = currentSettlementComponentId;
        BoughtItems = boughtItems;
        SoldItems = soldItems;
    }
}
