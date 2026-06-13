using GameInterface.Services.Heroes.Interfaces;
using System;
using System.Collections.Generic;
using Xunit;

namespace Coop.Tests.GameInterface.Services.Heroes;

/// <summary>
/// Tests for the server-side StringId allocation in HeroInterface: a newly created client must not be
/// assigned an id that a registered (possibly saved-but-absent) player already owns.
/// </summary>
public class HeroInterfaceTests
{
    // Models the campaign id allocator for the saved-but-unloaded case the fix targets: the registered
    // objects don't exist, so the next "campaign-unique" id for a seed is simply the seed itself.
    private static readonly Func<string, string> CampaignSeedFree = seed => seed;

    [Theory]
    [InlineData("Player", "Player1")]
    [InlineData("Player1", "Player2")]
    [InlineData("Player9", "Player10")]
    [InlineData("Player430", "Player431")]
    [InlineData("Player2863", "Player2864")]
    public void IncrementPostfix_AdvancesTrailingNumber(string input, string expected)
    {
        Assert.Equal(expected, HeroInterface.IncrementPostfix(input));
    }

    [Fact]
    public void NextUnreservedStringId_NoReservedIds_ReturnsCampaignId()
    {
        var id = HeroInterface.NextUnreservedStringId(CampaignSeedFree, new HashSet<string>());

        Assert.Equal("Player", id);
    }

    [Fact]
    public void NextUnreservedStringId_BaseIdReservedByAbsentPlayer_SkipsToNextFreeId()
    {
        // The reported bug: a saved-but-absent player still owns "Player", so a freshly created client must
        // not be handed "Player" — both records would otherwise resolve to one party and double-count it.
        var reserved = new HashSet<string> { "Player" };

        var id = HeroInterface.NextUnreservedStringId(CampaignSeedFree, reserved);

        Assert.Equal("Player1", id);
    }

    [Fact]
    public void NextUnreservedStringId_ConsecutiveReservedIds_SkipsAllOfThem()
    {
        var reserved = new HashSet<string> { "Player", "Player1", "Player2" };

        var id = HeroInterface.NextUnreservedStringId(CampaignSeedFree, reserved);

        Assert.Equal("Player3", id);
    }

    [Fact]
    public void NextUnreservedStringId_UnrelatedReservedId_KeepsBaseId()
    {
        // A reserved id that isn't the base ("Player430") must not push the new client off "Player".
        var reserved = new HashSet<string> { "Player430" };

        var id = HeroInterface.NextUnreservedStringId(CampaignSeedFree, reserved);

        Assert.Equal("Player", id);
    }

    [Fact]
    public void NextUnreservedStringId_RespectsBothCampaignAndReserved()
    {
        // The campaign already has "Player" registered, so the allocator yields "Player1" for that seed;
        // "Player1" is also reserved by a player, so the result must step past both.
        Func<string, string> campaign = seed => seed == "Player" ? "Player1" : seed;
        var reserved = new HashSet<string> { "Player1" };

        var id = HeroInterface.NextUnreservedStringId(campaign, reserved);

        Assert.Equal("Player2", id);
    }
}
