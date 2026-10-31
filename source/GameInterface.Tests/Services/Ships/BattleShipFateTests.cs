using GameInterface.Services.Ships.Data;
using Xunit;

namespace GameInterface.Tests.Services.Ships;

/// <summary>M6 D4: the server drops a reported fate whose values would poison the campaign ship.</summary>
public class BattleShipFateTests
{
    [Theory]
    [InlineData("Ship_1", 500f, 200f, 10f, true)]
    [InlineData("Ship_1", 0f, 0f, 0f, true)]
    [InlineData(null, 500f, 200f, 0f, false)]
    [InlineData("", 500f, 200f, 0f, false)]
    [InlineData("Ship_1", float.NaN, 200f, 0f, false)]
    [InlineData("Ship_1", 500f, float.PositiveInfinity, 0f, false)]
    [InlineData("Ship_1", 500f, 200f, float.NaN, false)]
    [InlineData("Ship_1", 500f, 200f, -1f, false)]
    public void IsValid_OnlyForANamedShipWithFiniteValuesAndNoNegativeMitigatedDamage(string? shipId, float hitPoints,
        float sailHitPoints, float mitigatedDamage, bool expected)
    {
        Assert.Equal(expected, new BattleShipFate(shipId, hitPoints, sailHitPoints, sunk: false, mitigatedDamage).IsValid);
    }
}
