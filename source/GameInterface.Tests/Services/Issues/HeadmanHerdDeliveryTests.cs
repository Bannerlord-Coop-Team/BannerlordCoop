using Common.Util;
using GameInterface.Services.Issues.Interfaces;
using Moq;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using Xunit;

namespace GameInterface.Tests.Services.Issues;

using Quest = HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssueQuest;

public class HeadmanHerdDeliveryTests
{
    [Theory]
    [InlineData(0, 0, false)]
    [InlineData(2, 2, false)]
    [InlineData(2, 3, true)]
    [InlineData(3, 4, true)]
    public void DeliveryCountsAllModifiersButIgnoresOtherHerdTypes(int plain, int modified, bool expected)
    {
        var (quest, party) = CreateDelivery();
        party.ItemRoster.AddToCounts(quest._herdTypeToDeliver, plain);
        party.ItemRoster.AddToCounts(new EquipmentElement(quest._herdTypeToDeliver,
            ObjectHelper.SkipConstructor<ItemModifier>()), modified);
        party.ItemRoster.AddToCounts(ObjectHelper.SkipConstructor<ItemObject>(), 20);
        var delivery = new HeadmanHerdDeliveryInterface(Mock.Of<IHeadmanHerdQuestAuthority>());

        Assert.Equal(expected, delivery.CanDeliver(quest, party));
        Assert.True(delivery.CanReject(quest, party));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void NeitherOutcomeIsAllowedWhenPartyOrTargetHasLeft(bool partyLeft)
    {
        var (quest, party) = CreateDelivery();
        if (partyLeft) party._currentSettlement = null;
        else quest._targetHero._stayingInSettlement = null;
        var delivery = new HeadmanHerdDeliveryInterface(Mock.Of<IHeadmanHerdQuestAuthority>());

        Assert.False(delivery.CanDeliver(quest, party));
        Assert.False(delivery.CanReject(quest, party));
    }

    [Fact]
    public void CompletedQuestCannotBeDeliveredOrRejectedAgain()
    {
        var (quest, party) = CreateDelivery();
        quest._questState = QuestBase.QuestStates.Finalized;
        var delivery = new HeadmanHerdDeliveryInterface(Mock.Of<IHeadmanHerdQuestAuthority>());

        Assert.False(delivery.CanDeliver(quest, party));
        Assert.False(delivery.CanReject(quest, party));
    }

    private static (Quest, MobileParty) CreateDelivery()
    {
        var quest = ObjectHelper.SkipConstructor<Quest>();
        // Publicizer preserves readonly fields; initialize them only for this isolated fixture.
        AccessTools.Field(typeof(Quest), nameof(quest._targetSettlement)).SetValue(quest, ObjectHelper.SkipConstructor<Settlement>());
        AccessTools.Field(typeof(Quest), nameof(quest._targetHero)).SetValue(quest, ObjectHelper.SkipConstructor<Hero>());
        quest._targetHero._stayingInSettlement = quest._targetSettlement;
        AccessTools.Field(typeof(Quest), nameof(quest._herdTypeToDeliver)).SetValue(quest, ObjectHelper.SkipConstructor<ItemObject>());
        AccessTools.Field(typeof(Quest), nameof(quest._animalCountToDeliver)).SetValue(quest, 5);
        var party = ObjectHelper.SkipConstructor<MobileParty>();
        party._currentSettlement = quest._targetSettlement;
        party.Party = ObjectHelper.SkipConstructor<PartyBase>();
        party.Party.ItemRoster = new ItemRoster();
        return (quest, party);
    }
}
