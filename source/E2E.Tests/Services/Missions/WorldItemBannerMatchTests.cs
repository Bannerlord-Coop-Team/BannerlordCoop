using Missions.Agents.Handlers;
using Missions.Tournaments;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace E2E.Tests.Services.Missions;

public class WorldItemBannerMatchTests
{
    private const string OtherBannerCode = "11.45.126.4345.4345.764.764.1.0.0.462.0.13.512.512.764.764.1.0.0";

    public static IEnumerable<object[]> Matchers()
    {
        yield return new object[] { "drop", (Func<MissionWeapon, MissionWeapon, bool>)WeaponDropHandler.WeaponMatches };
        yield return new object[] { "pickup slot", (Func<MissionWeapon, MissionWeapon, bool>)WeaponPickupHandler.WeaponMatches };
        yield return new object[] { "pickup world item",
            (Func<MissionWeapon, MissionWeapon, bool>)((a, b) => WeaponPickupHandler.WorldItemMatchesPickup(a, b, true)) };
        yield return new object[] { "tournament", (Func<MissionWeapon, MissionWeapon, bool>)CoopTournamentController.RuntimeWeaponMatches };
    }

    [Theory]
    [MemberData(nameof(Matchers))]
    public void ReplayedArrowWithoutBanner_MatchesTheShooterCopy(string matcher, Func<MissionWeapon, MissionWeapon, bool> matches)
    {
        ItemObject arrows = MissileShotReplayTests.CreateItem("arrows", WeaponClass.Arrow,
            WeaponFlags.RangedWeapon | WeaponFlags.Consumable, usingTableau: false);
        var shooterCopy = new MissionWeapon(arrows, null, new Banner(MissileShotReplayTests.ClanBannerCode));
        var replayedCopy = new MissionWeapon(arrows, null, null);

        Assert.True(matches(shooterCopy, replayedCopy), matcher);
        Assert.True(matches(replayedCopy, shooterCopy), matcher);
    }

    [Theory]
    [MemberData(nameof(Matchers))]
    public void ShieldWithAnotherBanner_DoesntMatch(string matcher, Func<MissionWeapon, MissionWeapon, bool> matches)
    {
        ItemObject shield = MissileShotReplayTests.CreateItem("shield", WeaponClass.LargeShield,
            WeaponFlags.HasHitPoints, usingTableau: true);
        var clanShield = new MissionWeapon(shield, null, new Banner(MissileShotReplayTests.ClanBannerCode));

        Assert.True(matches(clanShield, new MissionWeapon(shield, null, new Banner(MissileShotReplayTests.ClanBannerCode))), matcher);
        Assert.False(matches(clanShield, new MissionWeapon(shield, null, new Banner(OtherBannerCode))), matcher);
        Assert.False(matches(clanShield, new MissionWeapon(shield, null, null)), matcher);
    }
}
