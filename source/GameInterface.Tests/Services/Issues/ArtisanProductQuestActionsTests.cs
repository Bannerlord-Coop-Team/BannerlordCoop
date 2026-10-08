using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using Xunit;

namespace GameInterface.Tests.Services.Issues;

public sealed class ArtisanProductQuestActionsTests
{
    [Theory]
    [InlineData(0, 60, 0, false)]
    [InlineData(0, 60, 1, true)]
    [InlineData(20, 60, 39, true)]
    [InlineData(20, 60, 40, false)]
    [InlineData(20, 60, 41, false)]
    [InlineData(60, 60, 1, false)]
    public void PartialDeliveryRequiresPositiveAmountBelowRemaining(int delivered, int required, int available, bool allowed)
        => Assert.Equal(allowed, ArtisanProductQuestActions.CanApply(
            ArtisanProductQuestAction.DeliverPartially, delivered, required, available, false));

    [Theory]
    [InlineData(20, 60, 39, false)]
    [InlineData(20, 60, 40, true)]
    [InlineData(20, 60, 100, true)]
    [InlineData(60, 60, 1, false)]
    [InlineData(-1, 60, 100, false)]
    public void FullDeliveryRequiresOnlyRemainingAmount(int delivered, int required, int available, bool allowed)
        => Assert.Equal(allowed, ArtisanProductQuestActions.CanApply(
            ArtisanProductQuestAction.DeliverFully, delivered, required, available, false));

    [Theory]
    [InlineData(ArtisanProductQuestAction.AcceptMerchantOffer)]
    [InlineData(ArtisanProductQuestAction.RefuseMerchantOffer)]
    public void MerchantCannotActAgainAfterRefusal(ArtisanProductQuestAction action)
    {
        Assert.True(ArtisanProductQuestActions.CanApply(action, 20, 60, 0, false));
        Assert.False(ArtisanProductQuestActions.CanApply(action, 20, 60, 0, true));
    }

    [Fact]
    public void RefusingDeliveryDoesNotRequireRemainingGoods()
        => Assert.True(ArtisanProductQuestActions.CanApply(ArtisanProductQuestAction.RefuseDelivery, 20, 60, 0, true));

    [Fact]
    public void UnknownActionIsRejected()
        => Assert.False(ArtisanProductQuestActions.CanApply((ArtisanProductQuestAction)99, 0, 60, 60, false));
}
