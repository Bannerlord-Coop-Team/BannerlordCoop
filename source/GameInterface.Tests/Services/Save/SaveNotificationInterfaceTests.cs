using GameInterface.Services.Save.Interfaces;
using Xunit;

namespace GameInterface.Tests.Services.Save;

public class SaveNotificationInterfaceTests
{
    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, true)]
    [InlineData(false, false, true)]
    public void ShouldApplySavingState_ShowsOnlyOnCampaignMapAndAlwaysClears(
        bool isSaving,
        bool isCampaignMapActive,
        bool expected)
    {
        Assert.Equal(
            expected,
            SaveNotificationInterface.ShouldApplySavingState(
                isSaving,
                isCampaignMapActive));
    }
}
