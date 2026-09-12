using GameInterface.Services.MapEventParties.Patches;
using Xunit;

namespace GameInterface.Tests.Services.MapEventParties;

/// <summary>
/// Tests the decision used by the MapEventParty troop state patches.
/// </summary>
public class MapEventPartyPatchesTests
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, true)]
    public void ShouldRunTroopStateUpdate_ReturnsExpectedResults(bool isOriginalAllowed, bool isServer, bool expected)
    {
        bool result = MapEventPartyPatches.ShouldRunTroopStateUpdate(isOriginalAllowed, isServer);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(false, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public void ShouldUseClientTroopStateFallback_ReturnsExpectedResults(
        bool battleSpawnEnabled,
        bool isCoopBattleActive,
        bool expected)
    {
        bool result = MapEventPartyPatches.ShouldUseClientTroopStateFallback(battleSpawnEnabled, isCoopBattleActive);
        Assert.Equal(expected, result);
    }
}
