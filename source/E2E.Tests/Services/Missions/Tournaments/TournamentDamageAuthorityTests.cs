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
}
