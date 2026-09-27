using GameInterface.Services.Tournaments.Data;
using Missions.Tournaments;

namespace E2E.Tests.Services.Missions.Tournaments;

public class TournamentDamageAuthorityTests
{
    [Fact]
    public void HumanAttackerOrigin_IsAcceptedOnEveryVictimCopy()
    {
        Guid attackerId = Guid.NewGuid();

        Assert.True(TournamentDamageAuthority.IsValidOrigin(
            "fighter-a",
            "fighter-b",
            attackerId,
            "fighter-a"));
    }

    [Fact]
    public void SpoofedAttackerOrigin_IsRejected()
    {
        Assert.False(TournamentDamageAuthority.IsValidOrigin(
            "spectator",
            "fighter-b",
            Guid.NewGuid(),
            "fighter-a"));
    }

    [Fact]
    public void EnvironmentalDamage_MustOriginateFromVictimAuthority()
    {
        Assert.True(TournamentDamageAuthority.IsValidOrigin(
            "fighter-b",
            "fighter-b",
            Guid.Empty,
            null));
        Assert.False(TournamentDamageAuthority.IsValidOrigin(
            "host",
            "fighter-b",
            Guid.Empty,
            null));
    }

    [Theory]
    [InlineData("remote-human", false, false, true)]
    [InlineData("remote-human", true, false, false)]
    [InlineData("remote-human", false, true, false)]
    [InlineData("source-human", false, false, false)]
    [InlineData("replaced-human", false, false, false)]
    [InlineData("npc", false, false, false)]
    [InlineData("human-without-controller", false, false, false)]
    [InlineData("missing", false, false, false)]
    public void PlayerReceivedDamage_AppliesOnlyToRemoteHumanStrikes(
        string victim,
        bool attackBlockedWithShield,
        bool isFallDamage,
        bool expected)
    {
        TournamentContestantData contestant = victim switch
        {
            "remote-human" => CreateContestant("friend", isHuman: true, isReplaced: false),
            "source-human" => CreateContestant("host", isHuman: true, isReplaced: false),
            "replaced-human" => CreateContestant("friend", isHuman: true, isReplaced: true),
            "npc" => CreateContestant("friend", isHuman: false, isReplaced: false),
            "human-without-controller" => CreateContestant(null, isHuman: true, isReplaced: false),
            _ => null
        };

        Assert.Equal(expected, TournamentDamageAuthority.ShouldApplyPlayerReceivedDamage(
            contestant,
            "host",
            attackBlockedWithShield,
            isFallDamage));
    }

    [Theory]
    [InlineData(36, 0.25f, 9)]
    [InlineData(1, 0.25f, 1)]
    [InlineData(3, 0.5f, 2)]
    [InlineData(36, 1f, 36)]
    [InlineData(0, 0.25f, 0)]
    [InlineData(5000, 1f, 2000)]
    public void ScalePlayerReceivedDamage_CeilsAndClampsLikeVanilla(
        int inflictedDamage,
        float multiplier,
        int expected)
    {
        Assert.Equal(expected, TournamentDamageAuthority.ScalePlayerReceivedDamage(
            inflictedDamage,
            multiplier));
    }

    private static TournamentContestantData CreateContestant(
        string controllerId,
        bool isHuman,
        bool isReplaced) =>
        new("slot", "character", 1, controllerId, "Name", isHuman, isReplaced, false, null);
}
