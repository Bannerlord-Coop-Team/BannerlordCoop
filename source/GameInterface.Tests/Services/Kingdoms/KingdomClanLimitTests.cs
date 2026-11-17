using Common.Util;
using GameInterface.Configuration;
using GameInterface.Services.Kingdoms;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using Xunit;

namespace GameInterface.Tests.Services.Kingdoms;

public class KingdomClanLimitTests
{
    private readonly KingdomClanLimit limit = new KingdomClanLimit();

    // The kingdom has its ruling clan, one vassal and one mercenary. The joining clan is a new one, like a companion's.
    [Theory]
    [InlineData(0, false, false, false)]
    [InlineData(3, false, false, false)]
    [InlineData(2, false, false, true)]
    [InlineData(2, false, true, false)]
    [InlineData(2, true, true, true)]
    [InlineData(3, true, false, true)]
    public void IsFull_CountsTheRulingClanAndVassals_AndMercenariesOnlyWhenTheHostCountsThem(
        int maxClans, bool countMercenaries, bool asMercenary, bool expectedFull)
    {
        var kingdom = CreateKingdom();
        AddClan(kingdom);
        AddClan(kingdom, isMercenary: true);

        Assert.Equal(expectedFull, limit.IsFull(null, kingdom, asMercenary, Options(maxClans, countMercenaries)));
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
        var clan = ObjectHelper.SkipConstructor<Clan>();
        clan._kingdom = kingdom;
        clan.IsUnderMercenaryService = isMercenary;
        kingdom._clans.Add(clan);
        return clan;
    }

    private static ModOptions Options(int maxClans, bool countMercenaries)
        => new ModOptions(new ModOptionsData
        {
            MaxClansPerKingdom = maxClans,
            CountMercenaries = countMercenaries,
        });
}
