using GameInterface.Services.Party;
using Common.Util;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using Xunit;

namespace GameInterface.Tests.Services.Party;

public class PlayerRansomReleaseSettlementProviderTests
{
    [Fact]
    public void GetReleaseSettlement_NoSafeDestination_ReturnsNearestDifferentSettlement()
    {
        var sellingParty = MobilePartyAtSettlement();
        var playerHero = ObjectHelper.SkipConstructor<Hero>();
        var fallbackSettlement = ObjectHelper.SkipConstructor<Settlement>();
        var callCount = 0;
        var provider = new PlayerRansomReleaseSettlementProvider(
            (settlement, navigationType, condition) =>
            {
                callCount++;
                return callCount == 1 ? null : fallbackSettlement;
            },
            (position, condition) => throw new InvalidOperationException("Point search was not expected."));

        var result = provider.GetReleaseSettlement(sellingParty, playerHero);

        Assert.Same(fallbackSettlement, result);
        Assert.Equal(2, callCount);
    }

    private static PartyBase MobilePartyAtSettlement()
    {
        var settlement = ObjectHelper.SkipConstructor<Settlement>();
        var mobileParty = ObjectHelper.SkipConstructor<MobileParty>();
        var party = ObjectHelper.SkipConstructor<PartyBase>();
        mobileParty.Party = party;
        mobileParty._currentSettlement = settlement;
        party.MobileParty = mobileParty;
        return party;
    }
}
