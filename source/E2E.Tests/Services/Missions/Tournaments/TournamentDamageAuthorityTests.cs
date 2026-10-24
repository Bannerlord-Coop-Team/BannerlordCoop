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
    [InlineData("remote-human", true)]
    [InlineData("local-human", false)]
    [InlineData("replaced-human", false)]
    [InlineData("npc", false)]
    [InlineData("human-without-controller", false)]
    [InlineData("missing", false)]
    public void IsRemotePlayer_OnlyForHumansOwnedByAnotherPeer(string victim, bool expected)
    {
        TournamentContestantData contestant = victim switch
        {
            "remote-human" => CreateContestant("friend", isHuman: true, isReplaced: false),
            "local-human" => CreateContestant("host", isHuman: true, isReplaced: false),
            "replaced-human" => CreateContestant("friend", isHuman: true, isReplaced: true),
            "npc" => CreateContestant("friend", isHuman: false, isReplaced: false),
            "human-without-controller" => CreateContestant(null, isHuman: true, isReplaced: false),
            _ => null
        };

        Assert.Equal(expected, TournamentDamageAuthority.IsRemotePlayer(contestant, "host"));
    }

    private static TournamentContestantData CreateContestant(
        string controllerId,
        bool isHuman,
        bool isReplaced) =>
        new("slot", "character", 1, controllerId, "Name", isHuman, isReplaced, false, null);
}
