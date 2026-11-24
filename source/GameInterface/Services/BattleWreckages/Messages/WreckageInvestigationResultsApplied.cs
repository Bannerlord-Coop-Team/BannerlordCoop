using Common.Messaging;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Localization;

namespace GameInterface.Services.BattleWreckages.Messages;

public readonly struct WreckageInvestigationResultsApplied : IEvent
{
    public readonly Hero MainHero;
    public readonly TroopRoster LootedTroops;
    public readonly ItemRoster LootedItems;
    public readonly List<TextObject> ConsequenceExplanations;
    public readonly int LootedGoldAmount;

    public WreckageInvestigationResultsApplied(
        Hero mainHero,
        TroopRoster lootedTroops,
        ItemRoster lootedItems,
        List<TextObject> consequenceExplanations,
        int lootedGoldAmount)
    {
        MainHero = mainHero;
        LootedTroops = lootedTroops;
        LootedItems = lootedItems;
        ConsequenceExplanations = consequenceExplanations;
        LootedGoldAmount = lootedGoldAmount;
    }
}
