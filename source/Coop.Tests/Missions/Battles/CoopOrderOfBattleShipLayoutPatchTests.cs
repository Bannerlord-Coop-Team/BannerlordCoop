#if DEBUG
using HarmonyLib;
using Missions.Naval;
using NavalDLC.ViewModelCollection.OrderOfBattle;
using Xunit;

namespace Coop.Tests.Missions.Battles;

[Collection("Mission.Current")]
public class CoopOrderOfBattleShipLayoutPatchTests
{
    [Fact]
    public void Target_IsTheOrderOfBattlesSavedShipLayoutLoad()
    {
        Assert.NotNull(AccessTools.DeclaredMethod(typeof(NavalOrderOfBattleVM), "LoadConfigurationShips"));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Prefix_SkipsTheSavedShipLayoutOnlyInACoopNavalBattle(bool coopNavalBattle, bool expectedVanillaLoad)
    {
        bool previous = CoopNavalMissionScope.IsActive;
        try
        {
            CoopNavalMissionScope.IsActive = coopNavalBattle;

            Assert.Equal(expectedVanillaLoad, CoopOrderOfBattleShipLayoutPatch.Prefix());
        }
        finally
        {
            CoopNavalMissionScope.IsActive = previous;
        }
    }
}
#endif
