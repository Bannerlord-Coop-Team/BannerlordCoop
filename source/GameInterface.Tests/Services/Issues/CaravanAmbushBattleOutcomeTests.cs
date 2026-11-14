using GameInterface.Services.Issues.Quests.CaravanAmbush;
using Xunit;

namespace GameInterface.Tests.Services.Issues;

public class CaravanAmbushBattleOutcomeTests
{
    [Theory]
    [InlineData(true, true, true, true, true, (int)CaravanAmbushBattleOutcome.CaravanLost)]
    [InlineData(false, false, false, true, true, (int)CaravanAmbushBattleOutcome.None)]
    [InlineData(false, true, true, true, true, (int)CaravanAmbushBattleOutcome.None)]
    [InlineData(false, true, false, false, true, (int)CaravanAmbushBattleOutcome.SurvivedWithoutHelp)]
    [InlineData(false, true, false, true, false, (int)CaravanAmbushBattleOutcome.DirectSuccess)]
    [InlineData(false, true, false, true, true, (int)CaravanAmbushBattleOutcome.HoldForReward)]
    public void ClassifiesTheBattleBeforeApplyingAnyQuestEffects(bool caravanLost, bool banditsLost,
        bool alreadySaved, bool ownerWon, bool caravanOnWinningSide, int expected)
    {
        Assert.Equal((CaravanAmbushBattleOutcome)expected, CaravanAmbushWorldEventService.Classify(caravanLost, banditsLost, alreadySaved, ownerWon, caravanOnWinningSide));
    }
}
