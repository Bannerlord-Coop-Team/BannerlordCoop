using GameInterface.Services.Issues.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.Issues;

using Quest = ArmyNeedsSuppliesIssueBehavior.ArmyNeedsSuppliesIssueQuest;

internal interface IArmyNeedsSuppliesDelivery
{
    bool CanDeliver(Quest quest, MobileParty party, byte supplies);
    void Deliver(Quest quest);
}

internal sealed class ArmyNeedsSuppliesDelivery : IArmyNeedsSuppliesDelivery
{
    internal const byte Grain = 1;
    internal const byte GrainAndLivestock = 2;
    internal const byte GrainAndWine = 3;
    internal const byte Everything = 4;

    public bool CanDeliver(Quest quest, MobileParty party, byte supplies)
    {
        if (quest == null || !quest.IsOngoing || party == null) return false;
        var army = quest.QuestGiver?.PartyBelongedTo?.Army;
        if (army == null || army.ArmyOwner != quest.QuestGiver) return false;

        return MeetsRequirements(supplies,
            party.ItemRoster.GetItemNumber(DefaultItems.Grain),
            party.ItemRoster.NumberOfLivestockAnimals,
            party.ItemRoster.GetItemNumber(MBObjectManager.Instance.GetObject<ItemObject>("wine")),
            quest._requestedGrainAmount, quest._requestedLiveStockAmount, quest._requestedWineAmount);
    }

    internal static bool MeetsRequirements(byte supplies, int grain, int livestock, int wine,
        int requiredGrain, int requiredLivestock, int requiredWine)
    {
        if (supplies < Grain || supplies > Everything) return false;
        if (requiredGrain < 0 || requiredLivestock < 0 || requiredWine < 0) return false;
        if (grain < requiredGrain) return false;
        if ((supplies == GrainAndLivestock || supplies == Everything) && livestock < requiredLivestock) return false;
        return (supplies != GrainAndWine && supplies != Everything) || wine >= requiredWine;
    }

    public void Deliver(Quest quest)
    {
        var supplies = QuestSuccessProofContext.Current;
        // The inventory may have changed since the dialog option was offered.
        if (!CanDeliver(quest, MobileParty.MainParty, supplies)) return;

        switch (supplies)
        {
            case Grain: quest.CollectedGrainConsequence(); break;
            case GrainAndLivestock: quest.CollectedGrainAndLiveStockConsequence(); break;
            case GrainAndWine: quest.CollectedGrainAndWineConsequence(); break;
            case Everything: quest.CollectedEverythingConsequence(); break;
        }
    }
}
