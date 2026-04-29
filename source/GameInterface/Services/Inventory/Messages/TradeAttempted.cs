using Common.Messaging;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace GameInterface.Services.Inventory.Messages;

public readonly struct TradeAttempted : IEvent
{
    public readonly ItemRoster FromRoster;
    public readonly ItemRoster ToRoster;
    public readonly bool IsTrading;
    public readonly bool IsDonating;
    public readonly Hero Hero;
    public readonly int TotalAmount;
    public readonly int MerchantGold;
    public readonly MobileParty Party;
    public readonly MobileParty CurrentMobileParty;
    public readonly SettlementComponent CurrentSettlementComponent;
    public readonly List<(ItemRosterElement, int)> BoughtItems;
    public readonly List<(ItemRosterElement, int)> SoldItems;

    public TradeAttempted(
        ItemRoster fromRoster,
        ItemRoster toRoster,
        bool isTrading,
        bool isDonating,
        Hero hero,
        int totalAmount,
        int merchantGold,
        MobileParty party,
        MobileParty currentMobileParty,
        SettlementComponent currentSettlementComponent,
        List<(ItemRosterElement, int)> boughtItems,
        List<(ItemRosterElement, int)> soldItems)
    {
        FromRoster = fromRoster;
        ToRoster = toRoster;
        IsTrading = isTrading;
        IsDonating = isDonating;
        Hero = hero;
        TotalAmount = totalAmount;
        MerchantGold = merchantGold;
        Party = party;
        CurrentMobileParty = currentMobileParty;
        CurrentSettlementComponent = currentSettlementComponent;
        BoughtItems = boughtItems;
        SoldItems = soldItems;
    }
}
