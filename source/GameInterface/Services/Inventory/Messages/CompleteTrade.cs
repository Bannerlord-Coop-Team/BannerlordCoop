using Common.Messaging;
using GameInterface.Services.Inventory.Data;
using GameInterface.Services.TroopRosters.Data;
using ProtoBuf;
using System.Collections.Generic;
using TaleWorlds.Core;

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
    public readonly ItemRosterElement[] FromItemRosterData;
    [ProtoMember(5)]
    public readonly ItemRosterElement[] ToItemRosterData;
    [ProtoMember(6)]
    public readonly Dictionary<string, EquipmentData[]> CharacterIdEquipmentsData;
    [ProtoMember(7)]
    public readonly bool IsTrading;
    [ProtoMember(8)]
    public readonly bool CanGainXpFromDiscarding;
    [ProtoMember(9)]
    public readonly string HeroId;
    [ProtoMember(10)]
    public readonly int TotalAmount;
    [ProtoMember(11)]
    public readonly int MerchantGold;
    [ProtoMember(12)]
    public readonly string PartyId;
    [ProtoMember(13)]
    public readonly bool IsSettlementComponentNull;
    [ProtoMember(14)]
    public readonly string CurrentSettlementComponentId;

    [ProtoMember(15)]
    public readonly (ItemRosterElementData, int)[] BoughtItems;
    [ProtoMember(16)]
    public readonly (ItemRosterElementData, int)[] SoldItems;

    [ProtoMember(17)]
    public readonly string TroopRosterId;
    [ProtoMember(18)]
    public readonly TroopRosterData TroopRosterData;

    public CompleteTrade(
        string fromItemRosterId,
        bool isFromItemRosterNull,
        string toItemRosterId,
        ItemRosterElement[] fromItemRosterData,
        ItemRosterElement[] toItemRosterData,
        Dictionary<string, EquipmentData[]> characterIdEquipmentsData,
        bool isTrading,
        bool canGainXpFromDiscarding,
        string heroId,
        int totalAmount,
        int merchantGold,
        string partyId,
        bool isSettlementComponentNull,
        string currentSettlementComponentId,
        (ItemRosterElementData, int)[] boughtItems,
        (ItemRosterElementData, int)[] soldItems,
        string troopRosterId,
        TroopRosterData troopRosterData)
    {
        FromItemRosterId = fromItemRosterId;
        IsFromItemRosterNull = isFromItemRosterNull;
        ToItemRosterId = toItemRosterId;
        FromItemRosterData = fromItemRosterData;
        ToItemRosterData = toItemRosterData;
        CharacterIdEquipmentsData = characterIdEquipmentsData;
        IsTrading = isTrading;
        CanGainXpFromDiscarding = canGainXpFromDiscarding;
        HeroId = heroId;
        TotalAmount = totalAmount;
        MerchantGold = merchantGold;
        PartyId = partyId;
        IsSettlementComponentNull = isSettlementComponentNull;
        CurrentSettlementComponentId = currentSettlementComponentId;
        BoughtItems = boughtItems;
        SoldItems = soldItems;
        TroopRosterId = troopRosterId;
        TroopRosterData = troopRosterData;
    }
}
