using Common.Util;
using GameInterface.Services.SiegeEvents.Interfaces;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit;

namespace GameInterface.Tests.Services.SiegeEvents;

public class ArmyMemberSiegeAftermathTests
{
    [Fact]
    public void IsArmyMemberAftermath_MemberOfWonSiege_IsTrue()
    {
        var settlement = ObjectHelper.SkipConstructor<Settlement>();

        Assert.True(SiegeEventInterface.IsArmyMemberAftermath(settlement, choiceSettlement: null, wasPlayerArmyMember: true));
    }

    [Fact]
    public void IsArmyMemberAftermath_LeaderWithTheChoice_IsFalse()
    {
        // the leader gets the devastate, pillage or mercy menu through the choice prompt instead
        var settlement = ObjectHelper.SkipConstructor<Settlement>();

        Assert.False(SiegeEventInterface.IsArmyMemberAftermath(settlement, settlement, wasPlayerArmyMember: true));
    }

    [Fact]
    public void IsArmyMemberAftermath_NotInAnArmyOrNoWonSiege_IsFalse()
    {
        var settlement = ObjectHelper.SkipConstructor<Settlement>();

        Assert.False(SiegeEventInterface.IsArmyMemberAftermath(settlement, choiceSettlement: null, wasPlayerArmyMember: false));
        Assert.False(SiegeEventInterface.IsArmyMemberAftermath(null, choiceSettlement: null, wasPlayerArmyMember: true));
    }
}