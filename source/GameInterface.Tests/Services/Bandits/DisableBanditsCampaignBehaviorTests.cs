using Common.Util;
using GameInterface.Services.Bandits.Patches;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit;

namespace GameInterface.Tests.Services.Bandits;

public class DisableBanditsCampaignBehaviorTests
{
    [Fact]
    public void HasCacheableHomeSettlement_BanditWithMissingHome_ReturnsFalse()
    {
        var party = ObjectHelper.SkipConstructor<MobileParty>();
        party.IsBandit = true;
        party.ActualClan = ObjectHelper.SkipConstructor<Clan>();

        var result = DisableBanditsCampaignBehavior.HasCacheableHomeSettlement(party);

        Assert.False(result);
    }

    [Fact]
    public void HasCacheableHomeSettlement_BanditWithHome_ReturnsTrue()
    {
        var party = ObjectHelper.SkipConstructor<MobileParty>();
        party.IsBandit = true;
        party.ActualClan = ObjectHelper.SkipConstructor<Clan>();
        party._customHomeSettlement = ObjectHelper.SkipConstructor<Settlement>();

        var result = DisableBanditsCampaignBehavior.HasCacheableHomeSettlement(party);

        Assert.True(result);
    }

    [Fact]
    public void HasCacheableHomeSettlement_NonBanditWithMissingHome_ReturnsTrue()
    {
        var party = ObjectHelper.SkipConstructor<MobileParty>();

        var result = DisableBanditsCampaignBehavior.HasCacheableHomeSettlement(party);

        Assert.True(result);
    }
}
