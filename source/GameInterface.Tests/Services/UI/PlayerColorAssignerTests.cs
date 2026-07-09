using GameInterface.Services.UI;
using System.Collections.Generic;
using TaleWorlds.Library;
using Xunit;

namespace GameInterface.Tests.Services.UI;

public class PlayerColorAssignerTests
{
    [Fact]
    public void GetColor_SamePlayer_ReturnsSameColorEveryTime()
    {
        var first = PlayerColorAssigner.GetColor("PlayerOne");
        var second = PlayerColorAssigner.GetColor("PlayerOne");

        Assert.Equal(first, second);
    }

    [Fact]
    public void GetColor_DifferentPlayers_ReturnsDifferentColors()
    {
        var colorOne = PlayerColorAssigner.GetColor("PlayerOne");
        var colorTwo = PlayerColorAssigner.GetColor("PlayerTwo");

        Assert.NotEqual(colorOne, colorTwo);
    }

    [Fact]
    public void GetColor_MorePlayersThanPaletteEntries_RepeatsRatherThanThrowing()
    {
        var seen = new HashSet<Color>();

        for (var i = 0; i < 100; i++)
        {
            seen.Add(PlayerColorAssigner.GetColor($"Player{i}"));
        }

        Assert.True(seen.Count > 1);
    }

    [Fact]
    public void GetColor_NullOrEmptyControllerId_ReturnsFallbackInsteadOfThrowing()
    {
        var nullColor = PlayerColorAssigner.GetColor(null);
        var emptyColor = PlayerColorAssigner.GetColor(string.Empty);

        Assert.Equal(nullColor, emptyColor);
    }
}
