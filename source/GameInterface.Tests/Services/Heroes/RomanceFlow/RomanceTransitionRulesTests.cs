using GameInterface.Services.Heroes.RomanceFlow;
using Romance = TaleWorlds.CampaignSystem.Romance;
using Xunit;

namespace GameInterface.Tests.Services.Heroes.RomanceFlow;

public class RomanceTransitionRulesTests
{
    [Theory]
    [InlineData(Romance.RomanceLevelEnum.Untested, Romance.RomanceLevelEnum.CourtshipStarted, true)]
    [InlineData(Romance.RomanceLevelEnum.MatchMadeByFamily, Romance.RomanceLevelEnum.CourtshipStarted, true)]
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
}
