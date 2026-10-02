using GameInterface.Services.Issues;
using Xunit;

namespace GameInterface.Tests.Services.Issues;

public class ArmyNeedsSuppliesDeliveryTests
{
    [Theory]
    [InlineData(1, 20, 0, 0, true)]
    [InlineData(2, 20, 3, 0, true)]
    [InlineData(3, 20, 0, 3, true)]
    [InlineData(4, 20, 3, 3, true)]
    [InlineData(1, 19, 30, 30, false)]
    [InlineData(2, 19, 30, 30, false)]
    [InlineData(3, 19, 30, 30, false)]
    [InlineData(4, 19, 30, 30, false)]
    [InlineData(2, 20, 2, 30, false)]
    [InlineData(3, 20, 30, 2, false)]
    [InlineData(4, 20, 2, 30, false)]
    [InlineData(4, 20, 30, 2, false)]
    [InlineData(0, 30, 30, 30, false)]
    [InlineData(5, 30, 30, 30, false)]
    public void DeliveryRequiresEverySelectedSupply(byte option, int grain, int livestock, int wine, bool expected)
    {
        Assert.Equal(expected, ArmyNeedsSuppliesDelivery.MeetsRequirements(option, grain, livestock, wine, 20, 3, 3));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void FullInventoryStillAllowsEachSeparateDelivery(byte option)
    {
        Assert.True(ArmyNeedsSuppliesDelivery.MeetsRequirements(option, 40, 6, 6, 20, 3, 3));
    }

    [Fact]
    public void VanillaZeroRequirementsRemainDeliverable()
    {
        Assert.True(ArmyNeedsSuppliesDelivery.MeetsRequirements(4, 0, 0, 0, 0, 0, 0));
    }

    [Theory]
    [InlineData(-1, 3, 3)]
    [InlineData(20, -1, 3)]
    [InlineData(20, 3, -1)]
    public void InvalidRequirementsCannotGrantAReward(int grain, int livestock, int wine)
    {
        Assert.False(ArmyNeedsSuppliesDelivery.MeetsRequirements(4, 40, 6, 6, grain, livestock, wine));
    }
}
