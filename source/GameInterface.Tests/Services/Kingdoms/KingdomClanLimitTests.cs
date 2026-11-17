using Common.Network;
using Common.Util;
using GameInterface.Configuration;
using GameInterface.Services.Kingdoms;
using GameInterface.Services.ObjectManager;
using Moq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using Xunit;

namespace GameInterface.Tests.Services.Kingdoms;

public class KingdomClanLimitTests
{
    private readonly KingdomClanLimit limit = new KingdomClanLimit(Mock.Of<INetwork>(), Mock.Of<IObjectManager>());

    // The kingdom has its ruling clan, one vassal, one mercenary and one eliminated clan that never counts.
    [Theory]
    [InlineData(0, false, false, false)]
    [InlineData(3, false, false, false)]
    [InlineData(2, false, false, true)]
    [InlineData(2, false, true, false)]
    [InlineData(2, true, true, true)]
    [InlineData(3, true, false, true)]
    [InlineData(4, true, false, false)]
    public void IsFull_CountsTheRulingClanAndVassals_AndMercenariesOnlyWhenTheHostCountsThem(
        int maxClans, bool countMercenaries, bool asMercenary, bool expectedFull)
    {
        var kingdom = CreateKingdom();
        AddClan(kingdom);
        AddClan(kingdom, isMercenary: true);
        AddClan(kingdom)._isEliminated = true;

        Assert.Equal(expectedFull, limit.IsFull(CreateClan(), kingdom, asMercenary, Options(maxClans, countMercenaries)));
    }

    [Fact]
    public void IsFull_KingdomOfOnlyItsRulingClan_IsFullAtOne()
    {
        var kingdom = CreateKingdom();

        Assert.True(limit.IsFull(CreateClan(), kingdom, asMercenary: false, Options(maxClans: 1, countMercenaries: false)));
    }

    // A clan the kingdom already counts is never refused. A mercenary becoming a vassal starts to count when
    // mercenaries don't, so a full kingdom refuses it.
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public void IsFull_ClanAlreadyInTheKingdom_IsRefusedOnlyWhenItStartsToCount(
        bool isMercenary, bool countMercenaries, bool expectedFull)
    {
        var kingdom = CreateKingdom();
        var clan = AddClan(kingdom, isMercenary);

        Assert.Equal(expectedFull, limit.IsFull(clan, kingdom, asMercenary: false, Options(maxClans: 1, countMercenaries)));
    }

    private static Kingdom CreateKingdom()
    {
        var kingdom = ObjectHelper.SkipConstructor<Kingdom>();
        kingdom._clans = new MBList<Clan>();
        kingdom._rulingClan = AddClan(kingdom);
        return kingdom;
    }

    private static Clan AddClan(Kingdom kingdom, bool isMercenary = false)
    {
        var clan = CreateClan();
        clan._kingdom = kingdom;
        clan.IsUnderMercenaryService = isMercenary;
        kingdom._clans.Add(clan);
        return clan;
    }

    private static Clan CreateClan() => ObjectHelper.SkipConstructor<Clan>();

    private static ModOptions Options(int maxClans, bool countMercenaries)
        => new ModOptions(new ModOptionsData
        {
            MaxClansPerKingdom = maxClans,
            CountMercenaries = countMercenaries,
        });
}
