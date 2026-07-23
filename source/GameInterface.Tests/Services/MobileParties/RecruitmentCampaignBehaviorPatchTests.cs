using GameInterface.Services.MobileParties.Patches;
using TaleWorlds.CampaignSystem;
using Xunit;

namespace GameInterface.Tests.Services.MobileParties;

public class RecruitmentCampaignBehaviorPatchTests
{
    [Theory]
    [InlineData(0, 9, 1000, 10, 9)]
    [InlineData(0, 9, 30, 10, 3)]
    [InlineData(0, 9, 0, 10, 0)]
    [InlineData(0, 9, 1000, 0, 0)]
    [InlineData(0, 0, 1000, 10, 0)]
    [InlineData(9, 8, 1000, 10, 8)]
    [InlineData(9, 12, 30, 10, 3)]
    [InlineData(9, 12, 0, 10, 0)]
    [InlineData(9, 12, 1000, 0, 0)]
    [InlineData(0, 12, 1000, 10, 12)]
    public void GetMercenaryHireCount_UsesRequestedStockAndAffordabilityMinimum(
        int selectedMercenaryCount,
        int availableMercenaries,
        int heroGold,
        int unitPrice,
        int expectedCount)
    {
        int count = RecruitmentCampaignBehaviorPatch.GetMercenaryHireCount(
            selectedMercenaryCount,
            availableMercenaries,
            heroGold,
            unitPrice);

        Assert.Equal(expectedCount, count);
    }

    [Fact]
    public void IsMercenaryStockChanged_SameTroopAndCount_ReturnsFalse()
    {
        var troop = new CharacterObject();

        bool changed = RecruitmentCampaignBehaviorPatch.IsMercenaryStockChanged(troop, 5, troop, 5);

        Assert.False(changed);
    }

    [Fact]
    public void IsMercenaryStockChanged_CountChanged_ReturnsTrue()
    {
        var troop = new CharacterObject();

        bool changed = RecruitmentCampaignBehaviorPatch.IsMercenaryStockChanged(troop, 5, troop, 4);

        Assert.True(changed);
    }

    [Fact]
    public void IsMercenaryStockChanged_TroopChanged_ReturnsTrue()
    {
        var previousTroop = new CharacterObject();
        var currentTroop = new CharacterObject();

        bool changed = RecruitmentCampaignBehaviorPatch.IsMercenaryStockChanged(previousTroop, 5, currentTroop, 5);

        Assert.True(changed);
    }
}
