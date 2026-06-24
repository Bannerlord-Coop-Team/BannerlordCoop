using GameInterface.Services.MobileParties.Patches;
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
}