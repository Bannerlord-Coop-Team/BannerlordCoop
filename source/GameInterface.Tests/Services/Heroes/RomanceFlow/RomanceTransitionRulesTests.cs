using GameInterface.Services.Heroes.RomanceFlow;
using Romance = TaleWorlds.CampaignSystem.Romance;
using Xunit;

namespace GameInterface.Tests.Services.Heroes.RomanceFlow;

public class RomanceTransitionRulesTests
{
    [Theory]
    [InlineData(Romance.RomanceLevelEnum.Untested, Romance.RomanceLevelEnum.CourtshipStarted, true)]
    [InlineData(Romance.RomanceLevelEnum.MatchMadeByFamily, Romance.RomanceLevelEnum.CourtshipStarted, true)]
    [InlineData(Romance.RomanceLevelEnum.CourtshipStarted, Romance.RomanceLevelEnum.CourtshipStarted, true)]
    [InlineData(Romance.RomanceLevelEnum.CoupleDecidedThatTheyAreCompatible, Romance.RomanceLevelEnum.CourtshipStarted, false)]
    [InlineData(Romance.RomanceLevelEnum.CourtshipStarted, Romance.RomanceLevelEnum.CoupleDecidedThatTheyAreCompatible, true)]
    [InlineData(Romance.RomanceLevelEnum.CoupleDecidedThatTheyAreCompatible, Romance.RomanceLevelEnum.CoupleAgreedOnMarriage, true)]
    [InlineData(Romance.RomanceLevelEnum.Untested, Romance.RomanceLevelEnum.CoupleAgreedOnMarriage, false)]
    [InlineData(Romance.RomanceLevelEnum.CourtshipStarted, Romance.RomanceLevelEnum.CoupleAgreedOnMarriage, false)]
    [InlineData(Romance.RomanceLevelEnum.CoupleAgreedOnMarriage, Romance.RomanceLevelEnum.Marriage, false)]
    [InlineData(Romance.RomanceLevelEnum.Ended, Romance.RomanceLevelEnum.CourtshipStarted, false)]
    public void IsAllowed_OnlyAcceptsVanillaCourtshipOrder(
        Romance.RomanceLevelEnum currentLevel,
        Romance.RomanceLevelEnum requestedLevel,
        bool expected)
    {
        Assert.Equal(expected, RomanceTransitionRules.IsAllowed(currentLevel, requestedLevel));
    }

    // v1.5.4 lets a failed courtship start again once its one season cooldown has passed.
    [Theory]
    [InlineData(Romance.RomanceLevelEnum.FailedInCompatibility, true, true)]
    [InlineData(Romance.RomanceLevelEnum.FailedInPracticalities, true, true)]
    [InlineData(Romance.RomanceLevelEnum.FailedInCompatibility, false, false)]
    [InlineData(Romance.RomanceLevelEnum.FailedInPracticalities, false, false)]
    [InlineData(Romance.RomanceLevelEnum.Rejection, true, false)]
    [InlineData(Romance.RomanceLevelEnum.Ended, true, false)]
    public void IsAllowed_RestartsAFailedCourtshipOnlyAfterItsCooldown(
        Romance.RomanceLevelEnum currentLevel,
        bool retryCooldownExpired,
        bool expected)
    {
        Assert.Equal(expected, RomanceTransitionRules.IsAllowed(
            currentLevel, Romance.RomanceLevelEnum.CourtshipStarted, retryCooldownExpired));
    }

    [Theory]
    [InlineData(Romance.RomanceLevelEnum.FailedInCompatibility, true)]
    [InlineData(Romance.RomanceLevelEnum.FailedInPracticalities, true)]
    [InlineData(Romance.RomanceLevelEnum.Rejection, false)]
    [InlineData(Romance.RomanceLevelEnum.CourtshipStarted, false)]
    public void IsFailedCourtship_MatchesTheLevelsVanillaRetries(Romance.RomanceLevelEnum level, bool expected)
    {
        Assert.Equal(expected, RomanceRetryCooldown.IsFailedCourtship(level));
    }
}
