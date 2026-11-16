using GameInterface.Services.Issues.Quests.LandLordCompanyOfTrouble;
using Xunit;

namespace GameInterface.Tests.Services.Issues;

public class LandLordCompanyOfTroubleServiceTests
{
    [Theory]
    [InlineData(false, false, false, false, (int)LandLordCompanyOfTroubleBattleEnd.Ignored)]
    [InlineData(false, true, true, true, (int)LandLordCompanyOfTroubleBattleEnd.Ignored)]
    [InlineData(true, false, false, true, (int)LandLordCompanyOfTroubleBattleEnd.MercenariesInOwnersParty)]
    [InlineData(true, false, false, false, (int)LandLordCompanyOfTroubleBattleEnd.MercenariesInOwnersParty)]
    [InlineData(true, true, false, true, (int)LandLordCompanyOfTroubleBattleEnd.Ignored)]
    [InlineData(true, true, true, true, (int)LandLordCompanyOfTroubleBattleEnd.OwnerWon)]
    [InlineData(true, true, true, false, (int)LandLordCompanyOfTroubleBattleEnd.OwnerLost)]
    public void BattleEnd_DependsOnWhoFoughtAndWhoWon(bool ownerInvolved, bool companyExists, bool companyInvolved, bool ownerWon, int expected)
    {
        Assert.Equal((LandLordCompanyOfTroubleBattleEnd)expected, LandLordCompanyOfTroubleService.ClassifyBattleEnd(ownerInvolved, companyExists, companyInvolved, ownerWon));
    }
}
