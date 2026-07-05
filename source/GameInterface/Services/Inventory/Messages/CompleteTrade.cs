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
    public readonly bool IsManagingWarehouse;
    [ProtoMember(10)]
    public readonly string HeroId;
    [ProtoMember(11)]
    public readonly string InitialHeroId;
    [ProtoMember(12)]
    public readonly int TotalAmount;
    [ProtoMember(13)]
    public readonly int MerchantGold;
    [ProtoMember(14)]
    public readonly string OwnerPartyId;
    [ProtoMember(15)]
    public readonly string CurrentMobilePartyId;
    [ProtoMember(16)]
    public readonly bool IsSettlementComponentNull;
    [ProtoMember(17)]
    public readonly string CurrentSettlementComponentId;

    [ProtoMember(18)]
    public readonly (ItemRosterElementData, int)[] BoughtItems;
    [ProtoMember(19)]
    public readonly (ItemRosterElementData, int)[] SoldItems;

    [ProtoMember(20)]
    public readonly string TroopRosterId;
    [ProtoMember(21)]
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
        bool isManagingWarehouse,
        string heroId,
        string initialHeroId,
        int totalAmount,
        int merchantGold,
        string ownerPartyId,
        string currentMobilePartyId,
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
        IsManagingWarehouse = isManagingWarehouse;
        HeroId = heroId;
        InitialHeroId = initialHeroId;
        TotalAmount = totalAmount;
        MerchantGold = merchantGold;
        OwnerPartyId = ownerPartyId;
        CurrentMobilePartyId = currentMobilePartyId;
        IsSettlementComponentNull = isSettlementComponentNull;
        CurrentSettlementComponentId = currentSettlementComponentId;
        BoughtItems = boughtItems;
        SoldItems = soldItems;
        TroopRosterId = troopRosterId;
        TroopRosterData = troopRosterData;
    }
}
