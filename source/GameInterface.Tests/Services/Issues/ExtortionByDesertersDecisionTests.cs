using GameInterface.Services.Issues.Quests.ExtortionByDeserters;
using Xunit;

namespace GameInterface.Tests.Services.Issues;

public class ExtortionByDesertersDecisionTests
{
    [Theory]
    [InlineData(false, false, false, false, (int)ExtortionRaidEnding.Cancel)]
    [InlineData(false, false, true, false, (int)ExtortionRaidEnding.Cancel)]
    [InlineData(false, true, false, false, (int)ExtortionRaidEnding.Fail3)]
    [InlineData(false, true, true, false, (int)ExtortionRaidEnding.Fail3)]
    [InlineData(true, false, false, false, (int)ExtortionRaidEnding.Fail2)]
    [InlineData(true, false, true, false, (int)ExtortionRaidEnding.None)]
    [InlineData(true, true, false, false, (int)ExtortionRaidEnding.Fail2)]
    [InlineData(true, true, true, false, (int)ExtortionRaidEnding.None)]
    [InlineData(false, false, false, true, (int)ExtortionRaidEnding.None)]
    [InlineData(false, true, false, true, (int)ExtortionRaidEnding.None)]
    [InlineData(true, false, false, true, (int)ExtortionRaidEnding.Fail2)]
    [InlineData(true, false, true, true, (int)ExtortionRaidEnding.Fail2)]
    public void ClassifiesRaidEndings(bool desertersRaid, bool ownerRaid, bool ownerInVillage,
        bool ownerLeftVillage, int expected)
    {
        Assert.Equal((ExtortionRaidEnding)expected,
            ExtortionByDesertersWorldEventService.ClassifyRaid(desertersRaid, ownerRaid, ownerInVillage, ownerLeftVillage));
    }

    [Theory]
    [InlineData(false, false, (int)ExtortionRunAwayOutcome.KeepFleeing)]
    [InlineData(false, true, (int)ExtortionRunAwayOutcome.TimedOut)]
    [InlineData(true, false, (int)ExtortionRunAwayOutcome.LostSight)]
    [InlineData(true, true, (int)ExtortionRunAwayOutcome.LostSight)]
    public void ClassifiesRunningAway(bool ownerLostDeserters, bool timedOut, int expected)
    {
        Assert.Equal((ExtortionRunAwayOutcome)expected,
            ExtortionByDesertersWorldEventService.ClassifyRunAway(ownerLostDeserters, timedOut));
    }
}
