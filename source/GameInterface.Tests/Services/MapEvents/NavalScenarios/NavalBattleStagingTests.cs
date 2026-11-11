#if DEBUG
using GameInterface.Services.MapEvents.NavalScenarios;
using Xunit;

namespace GameInterface.Tests.Services.MapEvents.NavalScenarios;

/// <summary>R51 B4: a captive's parked, emptied party was topped up and fielded, and its battle had no player hero.</summary>
public class NavalBattleStagingTests
{
    // A prisoner is refused with the command that frees them, whatever its parked party's state.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CaptivityRefusal_RefusesAPrisoner_WithTheReleaseCommand(bool partyActive)
    {
        string refusal = NavalBattleStaging.CaptivityRefusal("testclient1", "Player3325", isPrisoner: true, partyActive);

        Assert.Equal(
            "Player testclient1 is a prisoner; free them first with coop.debug.player_captivity.release_player Player3325.",
            refusal);
    }

    // A parked party is refused even when its hero is free, since it has nothing to field.
    [Fact]
    public void CaptivityRefusal_RefusesAnInactiveParty()
    {
        Assert.Equal("Player testclient1's party is inactive (parked), so it can't join a battle.",
            NavalBattleStaging.CaptivityRefusal("testclient1", "Player3325", isPrisoner: false, partyActive: false));
    }

    // A free hero with an active party is staged.
    [Fact]
    public void CaptivityRefusal_IsNullForAFreeHeroWithAnActiveParty()
    {
        Assert.Null(NavalBattleStaging.CaptivityRefusal("testclient1", "Player3325", isPrisoner: false, partyActive: true));
    }
}
#endif
